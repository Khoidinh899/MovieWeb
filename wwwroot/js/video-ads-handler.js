// =========================================================================
// MOONPHIM - VIDEO PLAYER & EPISODE HANDLER (VSMOV EMBED & DIRECT HLS)
// 100% Direct Playback, Resilient Event Delegation & Multi-Platform Support
// =========================================================================

(function () {
    let currentEpisodeIndex = -1;
    let currentHls = null;

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

    // Main Video Loader Function
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

        const videoContainer = document.getElementById('videoContainer');
        const videoPlayer = document.getElementById('moviePlayer');
        const embedPlayer = document.getElementById('embedPlayer');
        const heroButtons = document.getElementById('heroButtons');

        if (videoContainer) videoContainer.style.display = 'block';
        if (heroButtons) heroButtons.style.display = 'none';

        const ytIframe = document.getElementById('youtube-trailer-iframe');
        if (ytIframe) ytIframe.remove();

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

                if (window.Hls && window.Hls.isSupported()) {
                    const hls = new window.Hls({
                        maxBufferLength: 30,
                        maxMaxBufferLength: 60
                    });

                    hls.loadSource(src);
                    hls.attachMedia(videoPlayer);

                    hls.on(window.Hls.Events.MANIFEST_PARSED, () => {
                        if (startTime > 0) videoPlayer.currentTime = startTime;
                        videoPlayer.play().catch(() => {});
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

    // Play YouTube Trailer
    function playYouTubeTrailer(url) {
        const videoContainer = document.getElementById('videoContainer');
        const videoPlayer = document.getElementById('moviePlayer');
        const embedPlayer = document.getElementById('embedPlayer');

        if (videoContainer) videoContainer.style.display = 'block';

        let videoId = '';
        if (url.includes('watch?v=')) {
            videoId = url.split('watch?v=')[1].split('&')[0];
        } else if (url.includes('youtu.be/')) {
            videoId = url.split('youtu.be/')[1].split('?')[0];
        }

        if (!videoId) {
            if (typeof showNotification === 'function') {
                showNotification("Link trailer không hợp lệ!", "warning");
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
        if (embedPlayer) {
            embedPlayer.style.display = 'none';
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

    // === GLOBAL EVENT DELEGATION (Hoạt động 100% trong mọi trường hợp, không bị mất sự kiện) ===
    let isDelegationAttached = false;
    function attachGlobalDelegation() {
        if (isDelegationAttached) return;
        isDelegationAttached = true;

        document.addEventListener('click', function (e) {
            // 1. Bấm nút Chọn Tập (.episode-list-item)
            const epBtn = e.target.closest('.episode-list-item');
            if (epBtn) {
                e.preventDefault();
                e.stopPropagation();

                document.querySelectorAll('.episode-list-item').forEach(el => el.classList.remove('active'));
                epBtn.classList.add('active');

                const src = epBtn.getAttribute('data-url');
                const epIndex = parseInt(epBtn.getAttribute('data-index') || '0');
                const epName = epBtn.getAttribute('data-episode-name') || epBtn.textContent.trim();

                currentEpisodeIndex = epIndex;

                if (window.watchProgressTracker) {
                    const epNameMatch = epName.match(/\d+/);
                    if (epNameMatch) {
                        window.watchProgressTracker.episodeNumber = parseInt(epNameMatch[0]);
                    }
                }

                loadVideo(src);
                return;
            }

            // 2. Bấm nút Đổi Server (.server-tab)
            const serverTab = e.target.closest('.server-tab');
            if (serverTab) {
                e.preventDefault();
                e.stopPropagation();

                const serverKey = serverTab.dataset.server;
                document.querySelectorAll('.server-tab').forEach(t => t.classList.remove('active'));
                serverTab.classList.add('active');

                document.querySelectorAll('.episode-group').forEach(group => {
                    if (group.dataset.server === serverKey) {
                        group.style.display = 'block';
                    } else {
                        group.style.display = 'none';
                    }
                });

                if (window.watchProgressTracker) {
                    window.watchProgressTracker.serverName = serverKey;
                }
                return;
            }

            // 3. Bấm nút Xem Phim (#watchBtn)
            const watchBtn = e.target.closest('#watchBtn');
            if (watchBtn) {
                e.preventDefault();
                e.stopPropagation();

                // Ưu tiên tập 1 của server đang active
                const activeGroup = document.querySelector('.episode-group[style*="display: block"]') || document.querySelector('.episode-group');
                const firstEp = activeGroup ? activeGroup.querySelector('.episode-list-item') : document.querySelector('.episode-list-item');

                if (firstEp && firstEp.dataset.url) {
                    firstEp.click();
                    return;
                }

                const fallbackSource = window.episode1Url || window.movieMainUrl;
                if (fallbackSource) {
                    loadVideo(fallbackSource);
                } else {
                    if (typeof showNotification === 'function') {
                        showNotification("Không tìm thấy nguồn phát phim!", "warning");
                    }
                }
                return;
            }

            // 4. Bấm nút Trailer (#trailerBtn)
            const trailerBtn = e.target.closest('#trailerBtn');
            if (trailerBtn && !trailerBtn.disabled) {
                e.preventDefault();
                e.stopPropagation();
                if (!window.trailerUrl || window.trailerUrl.trim() === "") {
                    if (typeof showNotification === 'function') {
                        showNotification("Phim này chưa có trailer!", "info");
                    }
                    return;
                }
                playYouTubeTrailer(window.trailerUrl);
                return;
            }
        }, { passive: false });
    }

    // Keyboard seek ±10s
    document.addEventListener('keydown', (e) => {
        const target = e.target;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') return;

        const videoPlayer = document.getElementById('moviePlayer');
        if (!videoPlayer || !videoPlayer.duration || videoPlayer.style.display === 'none') return;

        if (e.key === 'ArrowLeft') {
            e.preventDefault();
            videoPlayer.currentTime = Math.max(0, videoPlayer.currentTime - 10);
        } else if (e.key === 'ArrowRight') {
            e.preventDefault();
            videoPlayer.currentTime = Math.min(videoPlayer.duration, videoPlayer.currentTime + 10);
        }
    });

    // Public init method
    window.initVideoPlayerHandler = function () {
        attachGlobalDelegation();
    };

    // Auto Run
    attachGlobalDelegation();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', attachGlobalDelegation);
    }
    window.addEventListener('pageshow', function () {
        attachGlobalDelegation();
    });
})();