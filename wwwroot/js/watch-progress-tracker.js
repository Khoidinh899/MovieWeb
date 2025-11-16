// ================================
// WATCH PROGRESS TRACKER - FIXED
// ================================

class WatchProgressTracker {
    constructor(movieId, episodeNumber = null) {
        this.movieId = movieId;
        this.episodeNumber = episodeNumber;
        this.videoPlayer = null;
        this.saveInterval = null;
        this.saveIntervalTime = 30000; // 30 seconds
        this.lastSavedTime = 0;
        this.resumePopup = null;
        this.resumeTime = null;
    }

    // Initialize
    async init() {
        this.videoPlayer = document.querySelector('video');
        if (!this.videoPlayer) {
            console.error('❌ Video player not found');
            return;
        }

        console.log('✅ Watch progress tracker initialized');
        console.log('🎬 Movie ID:', this.movieId);
        console.log('📺 Episode:', this.episodeNumber);

        if (this.resumeTime !== null) {
            this.seekToResumeTime();
        }

        if (this.resumeTime === null) {
            await this.checkResumeInfo();
        }

        this.startTracking();
    }

    // Seek to resume time when video is ready
    seekToResumeTime() {
        console.log('⏩ Seeking to resume time:', this.resumeTime);

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
            console.log('🔍 Checking resume info:', url);

            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include'
            });

            if (response.ok) {
                const data = await response.json();
                console.log('📊 Resume data:', data);

                if (data.hasHistory && data.watchedDuration > 10 && data.progressPercentage < 95) {
                    this.showResumePopup(data);
                }
            } else {
                console.log('ℹ️ No resume history found');
            }
        } catch (error) {
            console.error('❌ Error checking resume info:', error);
        }
    }

    // Show resume popup
    showResumePopup(data) {
        const minutes = Math.floor(data.watchedDuration / 60);
        const seconds = data.watchedDuration % 60;
        const timeString = `${minutes}:${seconds.toString().padStart(2, '0')}`;
        const episodeText = data.episodeNumber 
            ? `của <strong>Tập ${data.episodeNumber}</strong>` 
            : 'của phim này';

        console.log('🎯 Showing resume popup');

        const existingPopup = document.getElementById('resumePopup');
        if (existingPopup) existingPopup.remove();

        const popupDiv = document.createElement('div');
        popupDiv.id = 'resumePopup';
        popupDiv.className = 'resume-popup';
        popupDiv.innerHTML = `
            <div class="resume-content">
                <div class="resume-header">
                    <h4 class="resume-title"><i class="fas fa-play-circle"></i> Xem tiếp</h4>
                    <button class="resume-close" onclick="window.watchProgressTracker.closeResumePopup()">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="resume-body">
                    <p>Bạn đã xem đến <strong>${data.progressPercentage}%</strong> ${episodeText}</p>
                    <div class="resume-info">
                        <span>Thời gian: <span>${timeString}</span></span>
                        <span>${data.progressPercentage}%</span>
                    </div>
                    <div class="resume-progress-bar">
                        <div class="resume-progress-fill" style="width:${data.progressPercentage}%"></div>
                    </div>
                </div>
                <div class="resume-actions">
                    <button class="resume-btn resume-btn-secondary" onclick="window.watchProgressTracker.closeResumePopup()">
                        <i class="fas fa-redo"></i> Xem từ đầu
                    </button>
                    <button class="resume-btn resume-btn-primary" onclick="window.watchProgressTracker.resumePlayback(${data.watchedDuration}, ${data.episodeNumber})">
                        <i class="fas fa-play"></i> Xem tiếp (${timeString})
                    </button>
                </div>
            </div>
        `;

        document.body.appendChild(popupDiv);
        this.resumePopup = popupDiv;

        setTimeout(() => popupDiv.classList.add('show'), 100);
    }

    // Resume playback
    resumePlayback(time, episodeName = null) {
        console.log('▶️ Resuming playback at:', time, 'Episode:', episodeName);
        window.thoiGianXemTiep = time;
        console.log('HISTORY_LOG: 🚩 Đặt cờ thoiGianXemTiep =', time);
        this.resumeTime = time;
        this.closeResumePopup();

        const existingVideo = document.querySelector('video');

        if (existingVideo && existingVideo.readyState >= 2) {
            existingVideo.currentTime = time;
            existingVideo.play().catch(err => console.error('❌ Error playing video:', err));
            this.resumeTime = null;
            return;
        }

        let playButton = null;

        if (episodeName) {
            const episodes = document.querySelectorAll('.episode-list-item');
            playButton = [...episodes].find(ep => ep.dataset.episodeName == episodeName);
        }

        if (!playButton) playButton = document.getElementById('watchBtn');

        if (playButton) {
            playButton.click();

            let checkCount = 0;
            const checkVideo = setInterval(() => {
                checkCount++;
                const video = document.querySelector('video');

                if (video && video.src) {
                    clearInterval(checkVideo);

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
                } else if (checkCount >= 20) clearInterval(checkVideo);
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

        this.videoPlayer.addEventListener('ended', () => this.saveProgress(true));

        window.addEventListener('beforeunload', () => this.saveProgress());

        let pauseTimeout;
        this.videoPlayer.addEventListener('pause', () => {
            clearTimeout(pauseTimeout);
            pauseTimeout = setTimeout(() => this.saveProgress(), 2000);
        });
    }

   async saveProgress(isCompleted = false) {

    // ==== 💡 FIX LỖI QUẢNG CÁO GHI ĐÈ ====
    if (window.dangXemQuangCao === true) {
        console.log('HISTORY_LOG: 🚩 Đang xem quảng cáo, BỎ QUA lưu lịch sử.');
        return; // Không lưu lịch sử khi đang xem quảng cáo
    }
    // ==== KẾT THÚC FIX ====

    // Luôn tìm player mới nhất phòng trường hợp bị thay đổi khi đổi tập
    this.videoPlayer = document.querySelector("video");
    if (!this.videoPlayer) return;

    // =============================
    // 🔧 FIX LỖI: cập nhật số tập chính xác
    // =============================
    const activeButton = document.querySelector(".episode-list-item.active");

    if (activeButton?.dataset?.episodeName) {
        // Nếu có tập active → cập nhật
        this.episodeNumber = parseInt(activeButton.dataset.episodeName);
    } else {
        // Nếu không có active (phim lẻ), kiểm tra dữ liệu trong server
        const movieIdElement = document.querySelector("[data-movie-id]");

        // Chỉ reset về null nếu hoàn toàn KHÔNG có thuộc tính data-episode-number
        if (movieIdElement && !movieIdElement.hasAttribute("data-episode-number")) {
            this.episodeNumber = null;
        }
    }
    // =============================

    const currentTime = Math.floor(this.videoPlayer.currentTime);
    const duration = Math.floor(this.videoPlayer.duration);

    // Điều kiện không cần lưu
    if (currentTime < 10 || !duration || duration <= 0) return;
    if (Math.abs(currentTime - this.lastSavedTime) < 5 && !isCompleted) return;

    try {
        // Payload gửi lên server
        const data = {
            movieId: this.movieId,
            watchedDuration: currentTime,
            totalDuration: duration,
            isCompleted: isCompleted || currentTime / duration > 0.95
        };

        if (this.episodeNumber !== null && this.episodeNumber !== undefined) {
            data.episodeNumber = this.episodeNumber;
        }

        console.log("💾 Saving progress:", data);

        // CSRF Token
        const token = document.querySelector("#RequestVerificationToken")?.value;

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
            console.log(`✅ Progress saved: ${currentTime}/${duration}`);
        } else {
            const msg = await response.text();
            console.error("❌ Failed to save progress:", response.status, msg);
        }

    } catch (error) {
        console.error("❌ Error saving progress:", error);
    }
}

    stopTracking() {
        if (this.saveInterval) {
            clearInterval(this.saveInterval);
            this.saveInterval = null;
        }
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    const movieIdElement = document.querySelector('[data-movie-id]');

    if (movieIdElement) {
        const movieId = parseInt(movieIdElement.dataset.movieId);
        const episodeNumber = movieIdElement.dataset.episodeNumber ? parseInt(movieIdElement.dataset.episodeNumber) : null;

        window.watchProgressTracker = new WatchProgressTracker(movieId, episodeNumber);

        let checkCount = 0;
        const checkVideo = setInterval(() => {
            checkCount++;
            const video = document.querySelector('video');

            if (video) {
                clearInterval(checkVideo);
                window.watchProgressTracker.init();
            } else if (checkCount >= 20) {
                clearInterval(checkVideo);
                console.log('ℹ️ Waiting for video player...');
            }
        }, 500);
    }
});
