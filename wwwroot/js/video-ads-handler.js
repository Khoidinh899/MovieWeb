// ============================================
// VIDEO ADS HANDLER - FIXED LOGIC
// ✅ Sửa: Frame video load trước, nút Xem phim/Trailer đúng logic, active badge, design nút Next
// ============================================

document.addEventListener('DOMContentLoaded', function () {

    // === 1. LẤY CÁC PHẦN TỬ DOM ===
    const videoPlayer = document.getElementById('moviePlayer');
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

    console.log('🎬 Video Ads Handler khởi động:', {
        shouldShowAds,
        isSeriesType,
        trailerUrl,
        movieMainUrl,
        episode1Url,
        episodeCount: episodeButtons.length
    });

    // === 3. KHỞI TẠO ===
    function init() {
        buildEpisodeList();
        attachEpisodeListeners();
        attachWatchButtonListener();
        attachTrailerButtonListener();
        attachKeyboardListeners();
        attachVideoPlayerListeners();
        loadBannerAds();

        console.log('📋 Đã load', allEpisodes.length, 'tập phim');
    }

    // === 4. BUILD DANH SÁCH TẬP PHIM (ĐÃ SẮP XẾP ĐÚNG) ===
    function buildEpisodeList() {
        const tempEpisodes = [];

        episodeButtons.forEach((btn) => {
            const episodeSrc = btn.getAttribute('data-url');
            const episodeIndex = parseInt(btn.getAttribute('data-index'));
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

        console.log('✅ Danh sách tập đã sort:', allEpisodes.map(e => e.name));
    }

    // === 5. GẮN SỰ KIỆN CHO NÚT "XEM PHIM" ===
    function attachWatchButtonListener() {
        if (!watchBtn) return;

        watchBtn.addEventListener('click', async () => {
            console.log('🎯 Bấm nút "Xem phim"');

            // ✅ ẨN CẢ 2 NÚT SAU KHI BẤM
            if (heroButtons) {
                heroButtons.style.display = 'none';
            }

            // ✅ HIỂN THỊ VIDEO CONTAINER TRƯỚC
            videoContainer.style.display = 'block';
            videoPlayer.scrollIntoView({ behavior: 'smooth', block: 'center' });
            const iframe = document.getElementById('youtube-trailer-iframe');
            if (iframe) {
                iframe.remove(); // Xóa hẳn iframe để tắt nhạc
            }
            videoPlayer.style.display = 'block';

            // ✅ NẾU LÀ PHIM BỘ: Phát tập 1
            if (isSeriesType) {
                const ep1Source = episode1Url || (allEpisodes.length > 0 ? allEpisodes[0].src : null);

                if (!ep1Source) {
                    alert('Không tìm thấy tập 1!');
                    return;
                }

                console.log('📺 Phim bộ → Phát tập 1:', ep1Source);
                await attemptToPlayEpisode(0, ep1Source);
                return;
            }

            // ✅ NẾU LÀ PHIM LẺ: Phát từ TrailerUrl (M3U8 phim chính)
            if (!isSeriesType && movieMainUrl) {
                console.log('🎬 Phim lẻ → Phát từ TrailerUrl:', movieMainUrl);
                await playMovieDirectly(movieMainUrl);
                return;
            }

            alert('Không tìm thấy nguồn phim!');
        });
    }

    // === 6. GẮN SỰ KIỆN CHO NÚT "TRAILER" ===
    function attachTrailerButtonListener() {
        if (!trailerBtn || trailerBtn.disabled) return;

        trailerBtn.addEventListener('click', () => {
            console.log('🎬 Bấm nút "Trailer"');

            if (!trailerUrl || trailerUrl.trim() === "") {
                alert('Phim này chưa có trailer!');
                return;
            }

            playYouTubeTrailer(trailerUrl);
        });
    }

    // === 7. PHÁT YOUTUBE TRAILER (EMBED) ===
    function playYouTubeTrailer(url) {
        console.log('▶️ Phát YouTube Trailer:', url);

        videoContainer.style.display = 'block';
        videoPlayer.scrollIntoView({ behavior: 'smooth', block: 'center' });

        // Convert YouTube URL to embed
        let videoId = '';
        if (url.includes('watch?v=')) {
            videoId = url.split('watch?v=')[1].split('&')[0];
        } else if (url.includes('youtu.be/')) {
            videoId = url.split('youtu.be/')[1].split('?')[0];
        }

        if (!videoId) {
            alert('Link trailer không hợp lệ!');
            return;
        }

        // Destroy HLS nếu có
        if (currentHls) {
            currentHls.destroy();
            currentHls = null;
        }

        // Thay video player bằng iframe YouTube
        const videoWrapper = videoPlayer.parentElement;
        videoPlayer.style.display = 'none';

        let iframe = document.getElementById('youtube-trailer-iframe');
        if (!iframe) {
            iframe = document.createElement('iframe');
            iframe.id = 'youtube-trailer-iframe';
            iframe.style.width = '100%';
            iframe.style.height = '500px';
            iframe.style.border = 'none';
            iframe.style.borderRadius = '10px';
            iframe.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture';
            iframe.allowFullscreen = true;
            videoWrapper.insertBefore(iframe, videoPlayer);
        }

        iframe.src = `https://www.youtube.com/embed/${videoId}?autoplay=1`;
        iframe.style.display = 'block';
    }

    // === 8. GẮN SỰ KIỆN CHO CÁC NÚT TẬP ===
    function attachEpisodeListeners() {
        allEpisodes.forEach(episode => {
            episode.button.addEventListener('click', async (e) => {
                e.preventDefault();

                if (episode.index === currentEpisodeIndex) return;

                console.log('🎯 Click vào', episode.name);

                // ✅ ẨN NÚT XEM PHIM/TRAILER
                if (heroButtons) {
                    heroButtons.style.display = 'none';
                }

                // ✅ ẨN YOUTUBE IFRAME NẾU CÓ
                const iframe = document.getElementById('youtube-trailer-iframe');
                if (iframe) {
                    iframe.remove(); // Xóa hẳn iframe để tắt nhạc
                }
                videoPlayer.style.display = 'block';

                await attemptToPlayEpisode(episode.index, episode.src);
            });
        });
    }

    // === 9. HÀM PHÁT PHIM LẺ (CHỈ CẦN QUẢNG CÁO PREROLL) ===
    async function playMovieDirectly(src) {
        console.log('▶️ Phát phim lẻ:', src);

        // ✅ CHECK QUẢNG CÁO CHO PHIM LẺ (nếu user free)
        if (shouldShowAds) {
            try {
                const response = await fetch('/api/ads/get-placements?placements=PreRoll');
                if (response.ok) {
                    const ads = await response.json();
                    if (ads && ads.length > 0) {
                        console.log('📺 Hiển thị PreRoll Ad cho phim lẻ');
                        await showAdModal(ads[0]);
                    }
                }
            } catch (error) {
                console.error('❌ Lỗi API quảng cáo:', error);
            }
        }

        // Load video
        loadVideo(src);
    }

    // === 10. HÀM MASTER: KIỂM TRA & PHÁT VIDEO (CHO PHIM BỘ) ===
    async function attemptToPlayEpisode(index, src) {
        console.log(`🔍 Phát tập ${index + 1}...`);

        videoPlayer.pause();
        if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';

        // ✅ Nếu KHÔNG cần quảng cáo (Premium/Student) → Phát luôn
        if (!shouldShowAds) {
            console.log('✅ User Premium/Student, bỏ qua quảng cáo');
            loadAndPlayVideo(index, src);
            return;
        }

        // ✅ User Free → Hiển thị quảng cáo PreRoll
        try {
            const response = await fetch('/api/ads/get-placements?placements=PreRoll');
            if (!response.ok) throw new Error('API không phản hồi');

            const ads = await response.json();

            if (ads && ads.length > 0) {
                console.log('📺 Hiển thị PreRoll Ad:', ads[0].adName);
                // SỬA Ở ĐÂY: Thêm 3 dòng này để ép thoát fullscreen
                if (document.fullscreenElement) {
                    await document.exitFullscreen();
                }
                await showAdModal(ads[0]);
            }

        } catch (error) {
            console.error('❌ Lỗi API quảng cáo:', error);
        }

        loadAndPlayVideo(index, src);
    }

    // === 11. TẢI & PHÁT VIDEO ===
    function loadAndPlayVideo(index, src) {
        console.log(`▶️ Phát tập ${index + 1}:`, src);

        currentEpisodeIndex = index;
        videoPlayer.dataset.currentEpisodeIndex = index;
        hasPlayedClimaxAd = false;
        lastTimeUpdate = 0;

        videoContainer.style.display = 'block';
        videoPlayer.scrollIntoView({ behavior: 'smooth', block: 'center' });

        // Destroy HLS cũ
        if (currentHls) {
            currentHls.destroy();
            currentHls = null;
        }

        // ✅ Cập nhật UI - ĐỔI MÀU BADGE ACTIVE (giữ lại để đảm bảo)
        updateActiveBadge(index);

        // Ẩn nút next
        if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';

        loadVideo(src);
    }

    // === 12. LOAD VIDEO VỚI HLS ===
    function loadVideo(src) {
        if (Hls.isSupported()) {
            const hls = new Hls({
                maxBufferLength: 30,
                maxMaxBufferLength: 60
            });

            hls.loadSource(src);
            hls.attachMedia(videoPlayer);

            hls.on(Hls.Events.MANIFEST_PARSED, () => {
                videoPlayer.play().catch(e => {
                    console.warn('⚠️ Autoplay bị chặn:', e);
                });
            });

            hls.on(Hls.Events.ERROR, (event, data) => {
                if (data.fatal) {
                    console.error('❌ HLS fatal error:', data);
                }
            });

            currentHls = hls;

        } else if (videoPlayer.canPlayType('application/vnd.apple.mpegurl')) {
            videoPlayer.src = src;
            videoPlayer.play();
        } else {
            alert('Trình duyệt không hỗ trợ phát video .m3u8!');
        }
    }

    // === 13. HIỂN THỊ MODAL QUẢNG CÁO ===
    function showAdModal(ad) {
        return new Promise((resolve) => {
            if (!adModal || !adModalContent) {
                console.error('❌ Modal elements not found');
                resolve();
                return;
            }

            // ✅ KHÔNG LOCK SCROLL BODY
            adModal.style.display = 'flex';

            adModalContent.innerHTML = '';

            if (ad.adContentUrl.endsWith('.mp4') || ad.adContentUrl.endsWith('.webm')) {
                const video = document.createElement('video');
                video.src = ad.adContentUrl;
                video.autoplay = true;
                video.muted = true;
                video.style.width = '100%';
                video.style.height = '100%';
                video.style.objectFit = 'contain';
                adModalContent.appendChild(video);
            } else {
                const img = document.createElement('img');
                img.src = ad.adContentUrl;
                img.style.width = '100%';
                img.style.height = '100%';
                img.style.objectFit = 'contain';
                adModalContent.appendChild(img);
            }

            let countdown = 5;
            adSkipButton.disabled = true;
            adSkipButton.innerHTML = `Bỏ qua (<span id="skip-countdown">${countdown}</span>)`;

            const timer = setInterval(() => {
                countdown--;
                if (adCountdownTimer) adCountdownTimer.textContent = countdown;

                const currentSkipSpan = adSkipButton.querySelector('#skip-countdown');
                if (currentSkipSpan) currentSkipSpan.textContent = countdown;

                if (countdown <= 0) {
                    clearInterval(timer);
                    adSkipButton.disabled = false;
                    adSkipButton.innerHTML = '<i class="fas fa-forward"></i> Bỏ qua';
                }
            }, 1000);

            adSkipButton.onclick = () => {
                if (countdown <= 0) {
                    adModal.style.display = 'none';
                    clearInterval(timer);
                    resolve();
                }
            };
        });
    }

    // === 14. GẮN LOGIC PLAYER (CHUYỂN TẬP & QUẢNG CÁO) ===
    // SỬA: Đổi tên hàm
    function attachVideoPlayerListeners() {
        console.log('🔁 Kích hoạt logic player (Chuyển tập & QC Climax)');

        // ==========================================================
        // A. LOGIC CHỈ DÀNH CHO PHIM BỘ (Chuyển tập)
        // ==========================================================
        if (isSeriesType && allEpisodes.length > 1) {
            console.log('...Đang gắn logic chuyển tập (Phim bộ)');

            // ✅ TỰ ĐỘNG CHUYỂN TẬP KHI VIDEO KẾT THÚC
            videoPlayer.addEventListener('ended', async () => {
                console.log('🏁 Video kết thúc');
                nextEpisodeButton.disabled = true;
                const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                if (nextEpisode) {
                    console.log('⏭️ Tự động chuyển tập:', nextEpisode.name);
                    updateActiveBadge(nextEpisode.index);
                    if (nextEpisodeButton) nextEpisodeButton.style.display = 'none';
                    await attemptToPlayEpisode(nextEpisode.index, nextEpisode.src);
                } else {
                    console.log('🎬 Đã hết phim');
                    alert('Đã hết tập phim!');
                }
            });

            // ✅ GẮN SỰ KIỆN NÚT "TẬP TIẾP THEO"
            if (nextEpisodeButton) {
                nextEpisodeButton.addEventListener('click', async () => {
                    nextEpisodeButton.disabled = true; // SỬA: Thêm dòng chống spam
                    const nextEpisode = allEpisodes[currentEpisodeIndex + 1];

                    console.log('🎯 NÚT TẬP TIẾP THEO - Debug info:', {
                        currentIndex: currentEpisodeIndex,
                        nextIndex: currentEpisodeIndex + 1,
                        nextEpisode: nextEpisode,
                        totalEpisodes: allEpisodes.length
                    });

                    if (nextEpisode) {
                        console.log('🎯 Bấm nút "Tập tiếp theo":', nextEpisode.name);
                        updateActiveBadge(nextEpisode.index);
                        nextEpisodeButton.style.display = 'none';
                        await attemptToPlayEpisode(nextEpisode.index, nextEpisode.src);
                    }
                });
            }
        }

        // ==========================================================
        // B. LOGIC DÀNH CHO TẤT CẢ CÁC LOẠI PHIM (QC Climax, Nút Next)
        // ==========================================================
        videoPlayer.addEventListener('timeupdate', async () => {
            const currentTime = videoPlayer.currentTime;
            if (!videoPlayer.duration || videoPlayer.paused) return;

            // ✅ Hiển thị nút "Tập tiếp theo" (CHỈ PHIM BỘ)
            if (isSeriesType) {
                const showNextButtonTime = videoPlayer.duration - 120;
                if (nextEpisodeButton && currentTime >= showNextButtonTime && nextEpisodeButton.style.display === 'none') {
                    if (allEpisodes[currentEpisodeIndex + 1]) {
                        nextEpisodeButton.style.display = 'block';
                        nextEpisodeButton.disabled = false;
                    }
                }
            }

            // ✅ Quảng cáo Climax (DÀNH CHO CẢ PHIM BỘ VÀ LẺ)
            if (shouldShowAds && !hasPlayedClimaxAd) {
                const climaxTimeInSeconds = isSeriesType ? 600 : 1200; // Bộ: 10p, Lẻ: 20p
                const climaxTime = videoPlayer.duration - climaxTimeInSeconds;

                if (lastTimeUpdate < climaxTime && currentTime >= climaxTime) {
                    hasPlayedClimaxAd = true;
                    console.log(`🔥 Climax Ad (${isSeriesType ? 'Phim bộ @ 10p' : 'Phim lẻ @ 20p'})`);

                    videoPlayer.pause();
                    if (document.fullscreenElement) await document.exitFullscreen();

                    try {
                        const response = await fetch('/api/ads/get-placements?placements=ClimaxAd');
                        const ads = await response.json();
                        if (ads && ads.length > 0) {
                            console.log('📺 Hiển thị Climax Ad');
                            await showAdModal(ads[0]);
                        }
                    } catch (error) {
                        console.error('❌ Lỗi Climax Ad:', error);
                    }

                    videoPlayer.play();
                }
            }

            lastTimeUpdate = currentTime;
        });
    }

    // === ✅ HÀM CẬP NHẬT BADGE ACTIVE ===
    // (Hàm này giữ nguyên)
    function updateActiveBadge(index) {
        console.log('🔄 Đang cập nhật badge active cho index:', index);

        // Xóa tất cả active
        allEpisodes.forEach((ep) => {
            ep.button.classList.remove('active');
        });

        // Thêm active cho tập mới
        if (allEpisodes[index]) {
            allEpisodes[index].button.classList.add('active');
            console.log('✅ ĐÃ THÊM ACTIVE CHO:', allEpisodes[index].name);
            allEpisodes[index].button.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } else {
            console.error('❌ Không tìm thấy episode với index:', index);
        }
    }


    // === 15. TẢI BANNER QUẢNG CÁO (CHỈ CHO USER FREE) ===
    async function loadBannerAds() {
        const watchPageBannerSlot = document.getElementById('watchpage-banner-slot');

        if (watchPageBannerSlot && shouldShowAds) {
            try {
                const response = await fetch('/api/ads/get-placements?placements=WatchPage_Banner');
                const ads = await response.json();

                if (ads && ads.length > 0) {
                    const ad = ads[0];
                    watchPageBannerSlot.innerHTML = `
                        <a href="${ad.clickUrl}" target="_blank" rel="noopener noreferrer" title="${ad.adName}">
                            <img src="${ad.adContentUrl}" alt="${ad.adName}" style="width: 100%; border-radius: 8px;" />
                        </a>`;
                    watchPageBannerSlot.style.display = 'block';
                }
            } catch (e) {
                console.error("❌ Lỗi tải banner:", e);
            }
        }
    }
    // === 16. GẮN PHÍM TẮT TUA VIDEO (10 GIÂY) ===
    function attachKeyboardListeners() {
        document.addEventListener('keydown', (e) => {
            // Bỏ qua nếu đang gõ chữ (ví dụ: ô bình luận)
            const target = e.target;
            if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                return;
            }
            // Chỉ chạy khi video đã được tải
            if (!videoPlayer || !videoPlayer.duration) return;

            switch (e.key) {
                case 'ArrowLeft':
                    e.preventDefault(); // Ngăn trình duyệt cuộn trang
                    console.log('⏪ Tua lùi 10s');
                    videoPlayer.currentTime = Math.max(0, videoPlayer.currentTime - 10);
                    break;
                case 'ArrowRight':
                    e.preventDefault(); // Ngăn trình duyệt cuộn trang
                    console.log('⏩ Tua tới 10s');
                    videoPlayer.currentTime = Math.min(videoPlayer.duration, videoPlayer.currentTime + 10);
                    break;
            }
        });
        console.log('⌨️ Đã gắn phím tắt tua video (10s)');
    }

    // === 17. KHỞI ĐỘNG ===
    init();
});