// =========================================================================
// MOONPHIM - VIDEO PLAYER & EPISODE HANDLER (VSMOV EMBED & DIRECT HLS)
// 100% Direct Playback, No Mock Ads Latency (Sole Ad Provider: Monetag)
// =========================================================================

document.addEventListener('DOMContentLoaded', function () {

    // === 1. DOM ELEMENTS ===
    const videoPlayer = document.getElementById('moviePlayer');
    const embedPlayer = document.getElementById('embedPlayer');
    const videoContainer = document.getElementById('videoContainer');
    const nextEpisodeButton = document.getElementById('nextEpisodeButton');
    const watchBtn = document.getElementById('watchBtn');
    const trailerBtn = document.getElementById('trailerBtn');
    const heroButtons = document.getElementById('heroButtons');
    const serverTabs = document.querySelectorAll('.server-tab');
    const episodeGroups = document.querySelectorAll('.episode-group');

    // === 2. BACKEND CONFIG ===
    const isSeriesType = window.isSeriesType ?? false;
    const trailerUrl = window.trailerUrl ?? "";
    const movieMainUrl = window.movieMainUrl ?? "";
    const episode1Url = window.episode1Url ?? "";

    // State Variables
    let currentEpisodeIndex = -1;
    let currentHls = null;
    let allEpisodes = [];

    // Helper: Detect Embed/Iframe URLs
    function isEmbedUrl(url) {
        if (!url) return false;
        const lower = url.toLowerCase();
        if (lower.includes('.m3u8')) return false;
        if (lower.includes('/video/') || lower.includes('/embed/') || lower.includes('streamvsmov') || lower.includes('vsmov') || lower.includes('player.phimapi.com') || lower.includes('youtube.com') || lower.includes('youtu.be')) return true;
        return !lower.includes('.m3u8');
    }

    // Helper: Build Embed URL with timestamp
    function buildEmbedUrl(url, startTime) {
        if (!url) return '';
        let target = url.trim();
        target = target.replace(/[?&]t=\d+/g, '').replace(/#t=\d+/g, '');
        const startSec = Math.floor(startTime || 0);
        if (startSec > 0) {
            const sep = target.includes('?') ? '&' : '?';
            target = `${target}${sep}t=${startSec}#t=${startSec}`;
        }
        return target;
    }

    // === 3. INITIALIZATION ===
    function init() {
        buildEpisodeList();
        attachServerTabListeners();
        attachEpisodeListeners();
        attachWatchButtonListener();
        attachTrailerButtonListener();
        attachKeyboardListeners();
        attachVideoPlayerListeners();
    }

    // === 4. BUILD EPISODE LIST ===
    function buildEpisodeList() {
        const tempEpisodes = [];
        const episodeButtons = document.querySelectorAll('.episode-list-item');

        episodeButtons.forEach((btn) => {
            const episodeSrc = btn.getAttribute('data-url');
            const episodeIndex = parseInt(btn.getAttribute('data-index') || '0');
            const episodeName = btn.getAttribute('data-episode-name') || btn.textContent.trim();

            const episodeNumberMatch = episodeName.match(/\d+/);
            const episodeNumber = episodeNumberMatch ? parseInt(episodeNumberMatch[0]) : episodeIndex;

            if (episodeSrc) {
                tempEpisodes.push({
                    index: episodeIndex,
                    number: episodeNumber,
                    name: episodeName,
                    src: episodeSrc,
                    button: btn
                });
            }
        });

        tempEpisodes.sort((a, b) => a.index - b.index);
        allEpisodes = tempEpisodes;
    }

    // === 5. SERVER TAB SWITCHING ===
    function attachServerTabListeners() {
        serverTabs.forEach(tab => {
            tab.addEventListener('click', function (e) {
                e.preventDefault();
                const serverKey = this.dataset.server;

                serverTabs.forEach(t => t.classList.remove('active'));
                this.classList.add('active');

                episodeGroups.forEach(group => {
                    if (group.dataset.server === serverKey) {
                        group.style.display = 'block';
                    } else {
                        group.style.display = 'none';
                    }
                });

                if (window.watchProgressTracker) {
                    window.watchProgressTracker.serverName = serverKey;
                }
            });
        });
    }

    // === 6. WATCH BUTTON (XEM PHIM) ===
    function attachWatchButtonListener() {
        if (!watchBtn) return;

        watchBtn.addEventListener('click', function (e) {
            e.preventDefault();
            // Ưu tiên tập 1 của server đang active
            const activeGroup = document.querySelector('.episode-group[style*="display: block"]') || document.querySelector('.episode-group');
            const firstEp = activeGroup ? activeGroup.querySelector('.episode-list-item') : document.querySelector('.episode-list-item');
            
            if (firstEp && firstEp.dataset.url) {
                firstEp.click();
                return;
            }

            const source = episode1Url || movieMainUrl || (allEpisodes.length > 0 ? allEpisodes[0].src : null);
            if (source) {
                if (heroButtons) heroButtons.style.display = 'none';
                loadVideo(source);
            } else {
                if (typeof showNotification === 'function') {
                    showNotification("Không tìm thấy nguồn phát phim!", "warning");
                }
            }
        });
    }

    // === 7. TRAILER BUTTON ===
    function attachTrailerButtonListener() {
        if (!trailerBtn || trailerBtn.disabled) return;

        trailerBtn.addEventListener('click', function (e) {
            e.preventDefault();
            if (!trailerUrl || trailerUrl.trim() === "") {
                if (window.MoonDialog) {
                    window.MoonDialog.alert({ title: 'Thông báo', message: 'Phim này chưa có trailer!', type: 'info' });
                } else {
                    alert('Phim này chưa có trailer!');
                }
                return;
            }

            playYouTubeTrailer(trailerUrl);
        });
    }

    // Play YouTube Trailer
    function playYouTubeTrailer(url) {
        if (videoContainer) videoContainer.style.display = 'block';

        let videoId = '';
        if (url.includes('watch?v=')) {
            videoId = url.split('watch?v=')[1].split('&')[0];
        } else if (url.includes('youtu.be/')) {
            videoId = url.split('youtu.be/')[1].split('?')[0];
        }

        if (!videoId) {
            if (window.MoonDialog) {
                window.MoonDialog.alert({ title: 'Thông báo', message: 'Link trailer không hợp lệ!', type: 'warning' });
            } else {
                alert('Link trailer không hợp lệ!');
            }
            return;
        }

        if (currentHls) {
            currentHls.destroy();
            currentHls = null;
        }

        if (videoPlayer) {
            videoPlayer.pause();
            videoPlayer.style.display = 'none';
        }

        const videoWrapper = (videoPlayer || embedPlayer)?.parentElement;
        let iframe = document.getElementById('youtube-trailer-iframe');
        if (!iframe && videoWrapper) {
            iframe = document.createElement('iframe');
            iframe.id = 'youtube-trailer-iframe';
            iframe.style.width = '100%';
            iframe.style.height = '500px';
            iframe.style.border = 'none';
            iframe.style.borderRadius = '10px';
            iframe.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture';
            iframe.allowFullscreen = true;
            videoWrapper.insertBefore(iframe, videoWrapper.firstChild);
        }

        if (iframe) {
            iframe.src = `https://www.youtube.com/embed/${videoId}?autoplay=1`;
            iframe.style.display = 'block';
            iframe.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
    }

    // === 8. EPISODE BUTTONS LISTENER (DIRECT & INSTANT) ===
    function attachEpisodeListeners() {
        allEpisodes.forEach(episode => {
            episode.button.addEventListener('click', function (e) {
                e.preventDefault();

                document.querySelectorAll('.episode-list-item').forEach(el => el.classList.remove('active'));
                episode.button.classList.add('active');

                if (heroButtons) {
                    heroButtons.style.display = 'none';
                }

                const ytIframe = document.getElementById('youtube-trailer-iframe');
                if (ytIframe) {
                    ytIframe.remove();
                }

                if (window.watchProgressTracker) {
                    const epNameMatch = episode.name.match(/\d+/);
                    if (epNameMatch) {
                        window.watchProgressTracker.episodeNumber = parseInt(epNameMatch[0]);
                    }
                }

                currentEpisodeIndex = episode.index;
                if (videoPlayer) videoPlayer.dataset.currentEpisodeIndex = episode.index;
                if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';

                // Phát trực tiếp ngay lập tức (Không chờ mock ads)
                loadVideo(episode.src);
            });
        });
    }

    // === 9. LOAD & PLAY VIDEO (VSMOV EMBED & DIRECT HLS) ===
    function loadVideo(src, startTime = 0) {
        if (!src || !src.trim()) {
            if (typeof showNotification === 'function') {
                showNotification("Tập này chưa có nguồn phát video!", "warning");
            }
            return;
        }
        src = src.trim();

        if (!startTime && window.thoiGianXemTiep && window.thoiGianXemTiep > 0) {
            startTime = window.thoiGianXemTiep;
            window.thoiGianXemTiep = 0;
        }

        if (videoContainer) videoContainer.style.display = 'block';

        if (isEmbedUrl(src)) {
            // === CHẾ ĐỘ EMBED (IFRAME VSMOV) ===
            if (currentHls) {
                currentHls.destroy();
                currentHls = null;
            }
            if (videoPlayer) {
                videoPlayer.pause();
                videoPlayer.removeAttribute('src');
                videoPlayer.style.display = 'none';
            }
            if (embedPlayer) {
                embedPlayer.style.display = 'block';
                const finalUrl = buildEmbedUrl(src, startTime);
                if (embedPlayer.src !== finalUrl) {
                    embedPlayer.src = finalUrl;
                }
                embedPlayer.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }

            if (window.watchProgressTracker) {
                window.watchProgressTracker.startEmbedTracking(startTime);
            }
        } else {
            // === CHẾ ĐỘ DIRECT HLS (.M3U8) ===
            if (embedPlayer) {
                embedPlayer.src = '';
                embedPlayer.style.display = 'none';
            }
            if (videoPlayer) {
                videoPlayer.style.display = 'block';
                videoPlayer.scrollIntoView({ behavior: 'smooth', block: 'center' });

                if (currentHls) {
                    currentHls.destroy();
                    currentHls = null;
                }

                if (Hls && Hls.isSupported()) {
                    const hls = new Hls({
                        maxBufferLength: 30,
                        maxMaxBufferLength: 60
                    });

                    hls.loadSource(src);
                    hls.attachMedia(videoPlayer);

                    hls.on(Hls.Events.MANIFEST_PARSED, () => {
                        if (startTime > 0) videoPlayer.currentTime = startTime;
                        videoPlayer.play().catch(() => {});
                    });

                    hls.on(Hls.Events.ERROR, (event, data) => {
                        if (data.fatal) {
                            console.error('❌ HLS fatal error:', data);
                        }
                    });

                    currentHls = hls;
                } else if (videoPlayer.canPlayType('application/vnd.apple.mpegurl')) {
                    videoPlayer.src = src;
                    if (startTime > 0) videoPlayer.currentTime = startTime;
                    videoPlayer.play().catch(() => {});
                }
            }

            if (window.watchProgressTracker) {
                window.watchProgressTracker.stopEmbedTracking();
                window.watchProgressTracker.videoPlayer = videoPlayer;
                window.watchProgressTracker.startTracking();
            }
        }
    }

    // Expose global method
    window.playVideo = loadVideo;

    // === 10. VIDEO PLAYER EVENT LISTENERS ===
    function attachVideoPlayerListeners() {
        if (isSeriesType && allEpisodes.length > 1) {
            if (videoPlayer) {
                videoPlayer.addEventListener('ended', () => {
                    if (nextEpisodeButton) nextEpisodeButton.disabled = true;
                    const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                    if (nextEpisode) {
                        nextEpisode.button.click();
                    }
                });
            }

            if (nextEpisodeButton) {
                nextEpisodeButton.addEventListener('click', () => {
                    nextEpisodeButton.disabled = true;
                    const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                    if (nextEpisode) {
                        nextEpisode.button.click();
                    }
                });
            }
        }

        if (videoPlayer) {
            videoPlayer.addEventListener('timeupdate', () => {
                const currentTime = videoPlayer.currentTime;
                if (!videoPlayer.duration || videoPlayer.paused) return;

                if (isSeriesType) {
                    const showNextButtonTime = videoPlayer.duration - 120;
                    if (nextEpisodeButton && currentTime >= showNextButtonTime && nextEpisodeButton.style.display === 'none') {
                        if (allEpisodes[currentEpisodeIndex + 1]) {
                            nextEpisodeButton.style.display = 'block';
                            nextEpisodeButton.disabled = false;
                        }
                    }
                }
            });
        }
    }

    // === 11. KEYBOARD SHORTCUTS (SEEK ±10s) ===
    function attachKeyboardListeners() {
        document.addEventListener('keydown', (e) => {
            const target = e.target;
            if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                return;
            }
            if (!videoPlayer || !videoPlayer.duration || videoPlayer.style.display === 'none') return;

            switch (e.key) {
                case 'ArrowLeft':
                    e.preventDefault();
                    videoPlayer.currentTime = Math.max(0, videoPlayer.currentTime - 10);
                    break;
                case 'ArrowRight':
                    e.preventDefault();
                    videoPlayer.currentTime = Math.min(videoPlayer.duration, videoPlayer.currentTime + 10);
                    break;
            }
        });
    }

    // Run Initialization
    init();
});