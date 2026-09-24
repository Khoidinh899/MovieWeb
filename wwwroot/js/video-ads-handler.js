// ============================================
// VIDEO ADS & PLAYBACK HANDLER (VSMOV EMBED & DIRECT HLS)
// ============================================

document.addEventListener('DOMContentLoaded', function () {

    // === 1. LẤY CÁC PHẦN TỬ DOM ===
    const videoPlayer = document.getElementById('moviePlayer');
    const embedPlayer = document.getElementById('embedPlayer');
    const videoContainer = document.getElementById('videoContainer');
    const nextEpisodeButton = document.getElementById('nextEpisodeButton');
    const episodeButtons = document.querySelectorAll('.episode-list-item');
    const watchBtn = document.getElementById('watchBtn');
    const trailerBtn = document.getElementById('trailerBtn');
    const heroButtons = document.getElementById('heroButtons');

    const adModal = document.getElementById('ad-modal');
    const adModalContent = document.getElementById('ad-modal-video-content');
    const adSkipButton = document.getElementById('ad-skip-button');
    const adCountdownTimer = document.getElementById('ad-countdown-timer');
    const skipCountdownSpan = document.getElementById('skip-countdown');

    // === 2. LẤY TRẠNG THÁI TỪ BACKEND ===
    const shouldShowAds = window.shouldShowAds ?? true;
    const isSeriesType = window.isSeriesType ?? false;
    const trailerUrl = window.trailerUrl ?? "";
    const movieMainUrl = window.movieMainUrl ?? "";
    const episode1Url = window.episode1Url ?? "";

    // Biến theo dõi
    let currentEpisodeIndex = -1;
    let hasPlayedClimaxAd = false;
    let currentHls = null;
    let allEpisodes = [];
    let lastTimeUpdate = 0;

    function isEmbedUrl(url) {
        if (!url) return false;
        const lower = url.toLowerCase();
        if (lower.includes('.m3u8')) return false;
        if (lower.includes('/video/') || lower.includes('/embed/') || lower.includes('streamvsmov') || lower.includes('vsmov') || lower.includes('player.phimapi.com') || lower.includes('youtube.com') || lower.includes('youtu.be')) return true;
        return !lower.includes('.m3u8');
    }

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

    // === 3. KHỞI TẠO ===
    function init() {
        buildEpisodeList();
        attachEpisodeListeners();
        attachWatchButtonListener();
        attachTrailerButtonListener();
        attachKeyboardListeners();
        attachVideoPlayerListeners();
        loadBannerAds();
    }

    // === 4. BUILD DANH SÁCH TẬP PHIM ===
    function buildEpisodeList() {
        const tempEpisodes = [];

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

    // === 5. GẮN SỰ KIỆN CHO NÚT "XEM PHIM" ===
    function attachWatchButtonListener() {
        if (!watchBtn) return;

        watchBtn.addEventListener('click', async (e) => {
            e.preventDefault();
            const firstEp = document.querySelector('.episode-list-item');
            if (firstEp && firstEp.dataset.url) {
                firstEp.click();
                return;
            }

            const source = episode1Url || movieMainUrl || (allEpisodes.length > 0 ? allEpisodes[0].src : null);
            if (source) {
                if (heroButtons) heroButtons.style.display = 'none';
                videoContainer.style.display = 'block';
                await playMovieDirectly(source);
            } else {
                if (typeof showNotification === 'function') {
                    showNotification("Không tìm thấy nguồn phim!", "warning");
                }
            }
        });
    }

    // === 6. GẮN SỰ KIỆN CHO NÚT "TRAILER" ===
    function attachTrailerButtonListener() {
        if (!trailerBtn || trailerBtn.disabled) return;

        trailerBtn.addEventListener('click', (e) => {
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

    // === 7. PHÁT YOUTUBE TRAILER (EMBED) ===
    function playYouTubeTrailer(url) {
        videoContainer.style.display = 'block';

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

    // === 8. GẮN SỰ KIỆN CHO CÁC NÚT TẬP ===
    function attachEpisodeListeners() {
        allEpisodes.forEach(episode => {
            episode.button.addEventListener('click', async (e) => {
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

                await attemptToPlayEpisode(episode.index, episode.src);
            });
        });
    }

    // === 9. HÀM PHÁT PHIM LẺ ===
    async function playMovieDirectly(src) {
        if (shouldShowAds) {
            try {
                const response = await fetch('/api/ads/get-placements?placements=PreRoll');
                if (response.ok) {
                    const ads = await response.json();
                    if (ads && ads.length > 0) {
                        await showAdModal(ads[0]);
                    }
                }
            } catch (error) {
                console.error('❌ Lỗi API quảng cáo:', error);
            }
        }

        loadVideo(src);
    }

    // === 10. HÀM MASTER: KIỂM TRA & PHÁT VIDEO ===
    async function attemptToPlayEpisode(index, src) {
        if (videoPlayer) videoPlayer.pause();
        if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';

        if (!shouldShowAds) {
            loadAndPlayEpisode(index, src);
            return;
        }

        try {
            const response = await fetch('/api/ads/get-placements?placements=PreRoll');
            if (response.ok) {
                const ads = await response.json();
                if (ads && ads.length > 0) {
                    if (document.fullscreenElement) {
                        await document.exitFullscreen();
                    }
                    await showAdModal(ads[0]);
                }
            }
        } catch (error) {
            console.error('❌ Lỗi API quảng cáo:', error);
        }

        loadAndPlayEpisode(index, src);
    }

    // === 11. TẢI & PHÁT TẬP PHIM ===
    function loadAndPlayEpisode(index, src) {
        currentEpisodeIndex = index;
        if (videoPlayer) videoPlayer.dataset.currentEpisodeIndex = index;
        hasPlayedClimaxAd = false;
        lastTimeUpdate = 0;

        videoContainer.style.display = 'block';
        updateActiveBadge(index);

        if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';

        loadVideo(src);
    }

    // === 12. LOAD VIDEO (HỖ TRỢ CẢ EMBED IFRAME VÀ DIRECT HLS) ===
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

        videoContainer.style.display = 'block';

        if (isEmbedUrl(src)) {
            // === CHẾ ĐỘ EMBED (IFRAME) ===
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

                if (Hls.isSupported()) {
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
                } else {
                    if (window.MoonDialog) {
                        window.MoonDialog.alert({ title: 'Lỗi phát video', message: 'Trình duyệt không hỗ trợ phát video định dạng này!', type: 'danger' });
                    }
                }
            }

            if (window.watchProgressTracker) {
                window.watchProgressTracker.stopEmbedTracking();
                window.watchProgressTracker.videoPlayer = videoPlayer;
                window.watchProgressTracker.startTracking();
            }
        }
    }

    window.playVideo = loadVideo;

    // === 13. HIỂN THỊ MODAL QUẢNG CÁO ===
    function showAdModal(ad) {
        return new Promise((resolve) => {
            window.dangXemQuangCao = true;

            if (!adModal || !adModalContent) {
                window.dangXemQuangCao = false;
                resolve();
                return;
            }

            adModal.style.display = 'flex';
            adModalContent.innerHTML = '';

            if (ad.adContentUrl && (ad.adContentUrl.endsWith('.mp4') || ad.adContentUrl.endsWith('.webm'))) {
                const video = document.createElement('video');
                video.src = ad.adContentUrl;
                video.autoplay = true;
                video.muted = true;
                video.style.width = '100%';
                video.style.height = '100%';
                video.style.objectFit = 'contain';
                adModalContent.appendChild(video);
            } else if (ad.adContentUrl) {
                const img = document.createElement('img');
                img.src = ad.adContentUrl;
                img.style.width = '100%';
                img.style.height = '100%';
                img.style.objectFit = 'contain';
                adModalContent.appendChild(img);
            }

            let countdown = 5;
            if (adSkipButton) {
                adSkipButton.disabled = true;
                adSkipButton.innerHTML = `Bỏ qua (<span id="skip-countdown">${countdown}</span>)`;
            }

            const timer = setInterval(() => {
                countdown--;
                if (adCountdownTimer) adCountdownTimer.textContent = countdown;

                const currentSkipSpan = adSkipButton?.querySelector('#skip-countdown');
                if (currentSkipSpan) currentSkipSpan.textContent = countdown;

                if (countdown <= 0) {
                    clearInterval(timer);
                    if (adSkipButton) {
                        adSkipButton.disabled = false;
                        adSkipButton.innerHTML = '<i class="fas fa-forward"></i> Bỏ qua';
                    }
                }
            }, 1000);

            if (adSkipButton) {
                adSkipButton.onclick = () => {
                    if (countdown <= 0) {
                        adModal.style.display = 'none';
                        clearInterval(timer);
                        window.dangXemQuangCao = false;
                        resolve();
                    }
                };
            }
        });
    }

    // === 14. GẮN LOGIC PLAYER (CHUYỂN TẬP & CLIMAX ADS CHO DIRECT VIDEO) ===
    function attachVideoPlayerListeners() {
        if (isSeriesType && allEpisodes.length > 1) {
            if (videoPlayer) {
                videoPlayer.addEventListener('ended', async () => {
                    if (nextEpisodeButton) nextEpisodeButton.disabled = true;
                    const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                    if (nextEpisode) {
                        updateActiveBadge(nextEpisode.index);
                        if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';
                        await attemptToPlayEpisode(nextEpisode.index, nextEpisode.src);
                    }
                });
            }

            if (nextEpisodeButton) {
                nextEpisodeButton.addEventListener('click', async () => {
                    nextEpisodeButton.disabled = true;
                    const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                    if (nextEpisode) {
                        updateActiveBadge(nextEpisode.index);
                        nextEpisodeButton.style.display = 'none';
                        await attemptToPlayEpisode(nextEpisode.index, nextEpisode.src);
                    }
                });
            }
        }

        if (videoPlayer) {
            videoPlayer.addEventListener('timeupdate', async () => {
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

                if (shouldShowAds && !hasPlayedClimaxAd) {
                    const climaxTimeInSeconds = isSeriesType ? 600 : 1200;
                    const climaxTime = videoPlayer.duration - climaxTimeInSeconds;

                    if (lastTimeUpdate < climaxTime && currentTime >= climaxTime) {
                        hasPlayedClimaxAd = true;

                        videoPlayer.pause();
                        if (document.fullscreenElement) await document.exitFullscreen();

                        try {
                            const response = await fetch('/api/ads/get-placements?placements=ClimaxAd');
                            if (response.ok) {
                                const ads = await response.json();
                                if (ads && ads.length > 0) {
                                    await showAdModal(ads[0]);
                                }
                            }
                        } catch (error) {
                            console.error('❌ Lỗi Climax Ad:', error);
                        }

                        videoPlayer.play().catch(() => {});
                    }
                }

                lastTimeUpdate = currentTime;
            });
        }
    }

    // === 15. HÀM CẬP NHẬT BADGE ACTIVE ===
    function updateActiveBadge(index) {
        allEpisodes.forEach((ep) => {
            ep.button.classList.remove('active');
        });

        if (allEpisodes[index]) {
            allEpisodes[index].button.classList.add('active');
            allEpisodes[index].button.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        }
    }

    // === 16. TẢI BANNER QUẢNG CÁO ===
    async function loadBannerAds() {
        const watchPageBannerSlot = document.getElementById('watchpage-banner-slot');

        if (watchPageBannerSlot && shouldShowAds) {
            try {
                const response = await fetch('/api/ads/get-placements?placements=WatchPage_Banner');
                if (response.ok) {
                    const ads = await response.json();
                    if (ads && ads.length > 0) {
                        const ad = ads[0];
                        watchPageBannerSlot.innerHTML = `
                            <a href="${ad.clickUrl}" target="_blank" rel="noopener noreferrer" title="${ad.adName}">
                                <img src="${ad.adContentUrl}" alt="${ad.adName}" style="width: 100%; border-radius: 8px;" />
                            </a>`;
                        watchPageBannerSlot.style.display = 'block';
                    }
                }
            } catch (e) {}
        }
    }

    // === 17. PHÍM TẮT TUA VIDEO ===
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

    // === 18. KHỞI ĐỘNG ===
    init();
});