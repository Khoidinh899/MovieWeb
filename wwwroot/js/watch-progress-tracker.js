// ===================================================
// WATCH PROGRESS TRACKER - CLEAN & USER FRIENDLY RESUME
// ===================================================

class WatchProgressTracker {
    constructor(movieId, episodeNumber = null) {
        this.movieId = movieId;
        this.episodeNumber = episodeNumber;
        this.serverName = null;
        this.videoPlayer = null;
        this.embedPlayer = null;
        this.saveInterval = null;
        this.saveIntervalTime = 12000; // 12 seconds
        this.lastSavedTime = 0;
        this.resumePopup = null;
        this.resumeTime = null;
        this.authToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || 
                         document.querySelector("#RequestVerificationToken")?.value;
        this.isLoggedIn = document.getElementById('notificationBell') !== null;

        // Embed iframe tracking properties
        this.isEmbedMode = false;
        this.embedAccumulatedSeconds = 0;
        this.embedLastTick = 0;
        this.embedTimer = null;
        this.embedTotalDuration = 2700;
        this.overlayClicked = false;
    }

    // Initialize
    async init() {
        if (!this.isLoggedIn) { 
            return; // Không đăng nhập -> Dừng
        }
        this.videoPlayer = document.querySelector('video');
        this.embedPlayer = document.getElementById('embedPlayer');

        if (this.resumeTime !== null) {
            this.seekToResumeTime();
        }

        if (this.resumeTime === null) {
            await this.checkResumeInfo();
        }

        this.startTracking();
    }

    // Tự động phát hiện ServerName hiện tại từ DOM
    getCurrentServerName() {
        const activeTab = document.querySelector('.server-tab.active');
        if (activeTab && activeTab.dataset.server) {
            return activeTab.dataset.server;
        }
        const activeButton = document.querySelector('.episode-list-item.active');
        if (activeButton) {
            const group = activeButton.closest('.episode-group');
            if (group && group.dataset.server) {
                return group.dataset.server;
            }
        }
        return this.serverName || null;
    }

    // Lớp phủ tàng hình bắt cú click Play đầu tiên trên Iframe
    setupIframeClickOverlay(startTime = 0) {
        const embedPlayer = document.getElementById('embedPlayer');
        if (!embedPlayer || embedPlayer.style.display === 'none') return;

        const parentWrapper = embedPlayer.parentElement;
        if (!parentWrapper) return;

        let overlay = document.getElementById('iframeClickOverlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'iframeClickOverlay';
            overlay.style.cssText = `
                position: absolute;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                z-index: 5;
                background: transparent;
                cursor: pointer;
            `;
            parentWrapper.style.position = 'relative';
            parentWrapper.appendChild(overlay);
        }

        overlay.style.display = 'block';
        this.overlayClicked = false;

        const handleOverlayClick = () => {
            this.overlayClicked = true;
            overlay.style.display = 'none';
            this.startEmbedTracking(startTime);
        };

        overlay.onclick = handleOverlayClick;
    }

    // Embed Tracking Timer
    startEmbedTracking(startTime = 0) {
        this.isEmbedMode = true;
        this.embedAccumulatedSeconds = startTime;
        this.embedLastTick = Date.now();
        this.lastSavedTime = startTime;

        if (this.embedTimer) clearInterval(this.embedTimer);

        this.embedTimer = setInterval(() => {
            if (!document.hidden && this.isEmbedMode) {
                const now = Date.now();
                const delta = (now - this.embedLastTick) / 1000;
                this.embedLastTick = now;
                if (delta > 0 && delta < 5) {
                    this.embedAccumulatedSeconds += delta;
                }
            } else {
                this.embedLastTick = Date.now();
            }
        }, 1000);

        this.startTracking();
    }

    stopEmbedTracking() {
        this.isEmbedMode = false;
        if (this.embedTimer) {
            clearInterval(this.embedTimer);
            this.embedTimer = null;
        }
    }

    // Seek to resume time when video is ready
    seekToResumeTime() {
        if (this.isEmbedMode) {
            this.embedAccumulatedSeconds = this.resumeTime || 0;
            this.resumeTime = null;
            return;
        }
        if (!this.videoPlayer) return;

        const seekWhenReady = () => {
            if (this.videoPlayer.readyState >= 2) {
                this.videoPlayer.currentTime = this.resumeTime;
                this.videoPlayer.play().catch(err => console.error('❌ Error playing video:', err));
                this.resumeTime = null;
            } else {
                this.videoPlayer.addEventListener('loadeddata', () => {
                    this.videoPlayer.currentTime = this.resumeTime;
                    this.videoPlayer.play().catch(err => console.error('❌ Error playing video:', err));
                    this.resumeTime = null;
                }, { once: true });
            }
        };

        seekWhenReady();
    }

    // Check resume info
    async checkResumeInfo() {
        try {
            const url = `/api/watch-history/resume/${this.movieId}${this.episodeNumber ? `?episodeNumber=${this.episodeNumber}` : ''}`;

            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include'
            });

            if (response.ok) {
                const data = await response.json();

                if (data.hasHistory && data.watchedDuration > 10 && data.progressPercentage < 95) {
                    this.showResumePopup(data);
                }
            }
        } catch (error) {
            console.error('❌ Error checking resume info:', error);
        }
    }

    // Show clean, friendly resume popup
    showResumePopup(data) {
        let episodeBadgeHtml = '';

        if (data.episodeNumber) {
            let serverText = data.serverName ? ` - Server ${data.serverName}` : '';
            episodeBadgeHtml = `<div class="resume-ep-badge"><i class="fas fa-play me-2"></i>Tập ${data.episodeNumber}${serverText}</div>`;
        } else {
            episodeBadgeHtml = `<div class="resume-ep-badge"><i class="fas fa-play me-2"></i>Phim này</div>`;
        }

        const existingPopup = document.getElementById('resumePopup');
        if (existingPopup) existingPopup.remove();

        const popupDiv = document.createElement('div');
        popupDiv.id = 'resumePopup';
        popupDiv.className = 'resume-popup';
        popupDiv.innerHTML = `
            <div class="resume-content">
                <div class="resume-header">
                    <h4 class="resume-title"><i class="fas fa-history text-warning me-1"></i> BẠN ĐANG XEM DỞ</h4>
                    <button class="resume-close" onclick="window.watchProgressTracker.closeResumePopup()" aria-label="Đóng">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="resume-body text-center">
                    <p class="resume-main-text">Hệ thống ghi nhận bạn đang xem dở:</p>
                    ${episodeBadgeHtml}
                    <p class="resume-sub-text mt-2 mb-0">Bạn có muốn tiếp tục xem tiếp không?</p>
                </div>
                <div class="resume-actions">
                    <button class="resume-btn resume-btn-secondary" onclick="window.watchProgressTracker.restartPlayback('${data.serverName || ''}')">
                        <i class="fas fa-redo"></i> Xem từ đầu
                    </button>
                    <button class="resume-btn resume-btn-primary" onclick="window.watchProgressTracker.resumePlayback(${data.watchedDuration || 0}, ${data.episodeNumber}, '${data.serverName || ''}')">
                        <i class="fas fa-play"></i> Xem tiếp
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(popupDiv);
        this.resumePopup = popupDiv;

        setTimeout(() => popupDiv.classList.add('show'), 100);
    }

    // Restart playback from beginning (Tập 1 hoặc từ đầu)
    restartPlayback(serverName = null) {
        this.closeResumePopup();

        if (serverName) {
            const serverTab = document.querySelector(`.server-tab[data-server="${serverName}"]`);
            if (serverTab) serverTab.click();
        }

        // Chọn tập 1 nếu là phim bộ
        let firstEpisodeBtn = null;
        if (serverName) {
            const group = document.querySelector(`.episode-group[data-server="${serverName}"]`);
            if (group) firstEpisodeBtn = group.querySelector('.episode-list-item');
        }
        if (!firstEpisodeBtn) {
            firstEpisodeBtn = document.querySelector('.episode-list-item');
        }

        if (firstEpisodeBtn) {
            firstEpisodeBtn.click();
        } else {
            const watchBtn = document.getElementById('watchBtn');
            if (watchBtn) watchBtn.click();
        }
    }

    // Resume playback (Kích hoạt lại đúng Server & Tập phim)
    resumePlayback(time, episodeName = null, serverName = null) {
        const effectiveTime = Math.max(0, time - 5);
        window.thoiGianXemTiep = effectiveTime;
        this.resumeTime = effectiveTime;
        this.closeResumePopup();

        // 1. Tự động bấm chọn lại Server Tab nếu có thông tin Server
        if (serverName) {
            const serverTab = document.querySelector(`.server-tab[data-server="${serverName}"]`);
            if (serverTab) {
                serverTab.click();
            }
        }

        const embedPlayer = document.getElementById('embedPlayer');
        const isEmbedActive = embedPlayer && embedPlayer.style.display !== 'none';

        if (isEmbedActive && window.playVideo) {
            const activeItem = document.querySelector('.episode-list-item.active');
            const currentUrl = activeItem ? activeItem.dataset.url : (window.episode1Url || window.trailerUrl);
            window.playVideo(currentUrl, time);
            this.setupIframeClickOverlay(time);
            if (typeof showNotification === 'function') {
                showNotification(`▶️ Đang mở Tập ${episodeName || 1}...`, "info");
            }
            return;
        }

        const existingVideo = document.querySelector('video');
        if (existingVideo && existingVideo.style.display !== 'none' && existingVideo.readyState >= 2) {
            existingVideo.currentTime = time;
            existingVideo.play().catch(err => console.error('❌ Error playing video:', err));
            this.resumeTime = null;
            return;
        }

        let playButton = null;

        if (episodeName) {
            let episodes = document.querySelectorAll('.episode-list-item');
            if (serverName) {
                const group = document.querySelector(`.episode-group[data-server="${serverName}"]`);
                if (group) {
                    episodes = group.querySelectorAll('.episode-list-item');
                }
            }
            playButton = [...episodes].find(ep => ep.dataset.episodeName == episodeName || ep.textContent.trim().includes(episodeName));
        }

        if (!playButton) playButton = document.getElementById('watchBtn');

        if (playButton) {
            window.thoiGianXemTiep = time;

            playButton.click();

            let checkCount = 0;
            const checkPlayer = setInterval(() => {
                checkCount++;
                const embed = document.getElementById('embedPlayer');
                const video = document.querySelector('video');

                if (embed && embed.style.display !== 'none') {
                    clearInterval(checkPlayer);
                    this.setupIframeClickOverlay(time);
                    if (typeof showNotification === 'function') {
                        showNotification(`▶️ Đang mở Tập ${episodeName || 1}...`, "info");
                    }
                } else if (video && video.src && video.style.display !== 'none') {
                    clearInterval(checkPlayer);

                    const seekWhenReady = () => {
                        if (video.readyState >= 2) {
                            video.currentTime = time;
                            video.play().catch(console.error);
                            this.resumeTime = null;
                            this.videoPlayer = video;
                            this.startTracking();
                        } else {
                            video.addEventListener('loadeddata', () => {
                                video.currentTime = time;
                                video.play().catch(console.error);
                                this.resumeTime = null;
                                this.videoPlayer = video;
                                this.startTracking();
                            }, { once: true });
                        }
                    };
                    seekWhenReady();
                } else if (checkCount >= 20) clearInterval(checkPlayer);
            }, 500);
        }
    }

    // Close resume popup
    closeResumePopup() {
        if (this.resumePopup) {
            this.resumePopup.classList.remove('show');
            setTimeout(() => {
                if (this.resumePopup) this.resumePopup.remove();
                this.resumePopup = null;
            }, 300);
        }
    }

    // Start tracking
    startTracking() {
        if (this.saveInterval) clearInterval(this.saveInterval);

        this.saveInterval = setInterval(() => this.saveProgress(), this.saveIntervalTime);

        if (this.videoPlayer) {
            this.videoPlayer.addEventListener('ended', () => this.saveProgress(true));

            let pauseTimeout;
            this.videoPlayer.addEventListener('pause', () => {
                clearTimeout(pauseTimeout);
                pauseTimeout = setTimeout(() => this.saveProgress(), 1500);
            });
        }

        window.addEventListener('beforeunload', () => this.saveProgress());
        window.addEventListener('pagehide', () => this.saveProgress());
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) this.saveProgress();
        });
    }

   async saveProgress(isCompleted = false) {
        if (!this.isLoggedIn || !this.authToken) {
             return; 
        }

        // Bỏ qua khi xem quảng cáo
        if (window.dangXemQuangCao === true) {
            return;
        }

        // Cập nhật số tập và Server đang xem
        const activeButton = document.querySelector(".episode-list-item.active");
        if (activeButton?.dataset?.episodeName) {
            this.episodeNumber = parseInt(activeButton.dataset.episodeName);
        } else {
            const movieIdElement = document.querySelector("[data-movie-id]");
            if (movieIdElement && !movieIdElement.hasAttribute("data-episode-number")) {
                this.episodeNumber = null;
            }
        }

        this.serverName = this.getCurrentServerName();

        let currentTime = 0;
        let duration = 0;

        const embedPlayer = document.getElementById("embedPlayer");
        const isEmbedActive = embedPlayer && embedPlayer.style.display !== "none" && embedPlayer.src;

        if (isEmbedActive || this.isEmbedMode) {
            currentTime = Math.floor(this.embedAccumulatedSeconds);
            duration = this.embedTotalDuration || 2700;
        } else {
            this.videoPlayer = document.querySelector("video");
            if (!this.videoPlayer) return;

            currentTime = Math.floor(this.videoPlayer.currentTime || 0);
            duration = Math.floor(this.videoPlayer.duration || 0);
        }

        if (currentTime < 10 || !duration || duration <= 0) return;
        if (Math.abs(currentTime - this.lastSavedTime) < 5 && !isCompleted) return;

        try {
            const data = {
                movieId: this.movieId,
                watchedDuration: currentTime,
                totalDuration: duration,
                isCompleted: isCompleted || (currentTime / duration > 0.95),
                serverName: this.serverName
            };

            if (this.episodeNumber !== null && this.episodeNumber !== undefined) {
                data.episodeNumber = this.episodeNumber;
            }

            const token = document.querySelector("#RequestVerificationToken")?.value || this.authToken;

            const response = await fetch("/api/watch-history", {
                method: "POST",
                credentials: "include",
                headers: {
                    "Content-Type": "application/json",
                    ...(token && { RequestVerificationToken: token })
                },
                body: JSON.stringify(data)
            });

            if (response.ok) {
                this.lastSavedTime = currentTime;
            }
        } catch (error) {
            console.error("❌ Error saving watch progress:", error);
        }
    }

    stopTracking() {
        if (this.saveInterval) {
            clearInterval(this.saveInterval);
            this.saveInterval = null;
        }
        this.stopEmbedTracking();
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    const isUserLoggedIn = document.getElementById('notificationBell');
    if (isUserLoggedIn && window.movieId) {
        window.watchProgressTracker = new WatchProgressTracker(window.movieId);
        window.watchProgressTracker.init();
    }
});
