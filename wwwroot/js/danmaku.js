/**
 * 🌙 MOONPHIM - DANMAKU BULLET COMMENTS ENGINE
 * High-performance 60fps Hardware Accelerated Danmaku layer
 */

class DanmakuEngine {
    constructor(container, options = {}) {
        this.container = typeof container === 'string' ? document.querySelector(container) : container;
        this.enabled = true;
        this.tracks = [];
        this.maxTracks = options.maxTracks || 8;
        this.trackHeight = options.trackHeight || 36;
        this.speed = options.speed || 8; // seconds to cross screen
        this.activeItems = new Set();
        this.isPaused = false;

        this.init();
    }

    init() {
        if (!this.container) return;
        this.container.classList.add('wp-danmaku-stage');
        this.container.style.position = 'absolute';
        this.container.style.inset = '0';
        this.container.style.pointerEvents = 'none';
        this.container.style.overflow = 'hidden';

        for (let i = 0; i < this.maxTracks; i++) {
            this.tracks.push({ index: i, nextAvailableTime: 0 });
        }

        window.addEventListener('resize', () => this.handleResize());
    }

    handleResize() {
        if (!this.container) return;
        const height = this.container.clientHeight;
        this.maxTracks = Math.max(3, Math.floor(height / this.trackHeight) - 1);
    }

    emit(text, color = '#ffffff', position = 'scroll') {
        if (!this.enabled || !this.container || !text) return;

        const item = document.createElement('div');
        item.className = `wp-danmaku-item ${position === 'top' ? 'top-fixed' : position === 'bottom' ? 'bottom-fixed' : 'scroll'}`;
        item.textContent = text;
        item.style.color = color;

        if (position === 'scroll') {
            this.renderScroll(item);
        } else if (position === 'top') {
            this.renderFixed(item, 'top');
        } else if (position === 'bottom') {
            this.renderFixed(item, 'bottom');
        }
    }

    renderScroll(item) {
        const stageWidth = this.container.clientWidth || 800;
        const now = Date.now();

        // Tìm track trống nhất
        let selectedTrack = this.tracks[0];
        let earliestTime = Infinity;

        for (let i = 0; i < Math.min(this.tracks.length, this.maxTracks); i++) {
            const track = this.tracks[i];
            if (track.nextAvailableTime <= now) {
                selectedTrack = track;
                break;
            }
            if (track.nextAvailableTime < earliestTime) {
                earliestTime = track.nextAvailableTime;
                selectedTrack = track;
            }
        }

        const topPos = selectedTrack.index * this.trackHeight + 10;
        item.style.top = `${topPos}px`;
        item.style.transform = `translateX(${stageWidth}px)`;
        item.style.transition = `transform ${this.speed}s linear`;

        this.container.appendChild(item);
        this.activeItems.add(item);

        // Đánh dấu thời gian khả dụng tiếp theo của track
        selectedTrack.nextAvailableTime = now + 1500;

        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                const itemWidth = item.offsetWidth || 150;
                item.style.transform = `translateX(-${itemWidth + 20}px)`;
            });
        });

        const timer = setTimeout(() => {
            item.remove();
            this.activeItems.delete(item);
        }, this.speed * 1000 + 200);

        item._timer = timer;
    }

    renderFixed(item, pos) {
        if (pos === 'top') {
            item.style.top = '20px';
        } else {
            item.style.bottom = '20px';
        }

        this.container.appendChild(item);
        this.activeItems.add(item);

        setTimeout(() => {
            item.remove();
            this.activeItems.delete(item);
        }, 4000);
    }

    pause() {
        this.isPaused = true;
        this.activeItems.forEach(item => {
            const computed = window.getComputedStyle(item);
            const matrix = computed.transform;
            item.style.transition = 'none';
            item.style.transform = matrix;
            if (item._timer) clearTimeout(item._timer);
        });
    }

    resume() {
        this.isPaused = false;
        // Danmaku resumed
    }

    clear() {
        this.activeItems.forEach(item => {
            if (item._timer) clearTimeout(item._timer);
            item.remove();
        });
        this.activeItems.clear();
    }

    toggle() {
        this.enabled = !this.enabled;
        if (!this.enabled) {
            this.clear();
            this.container.style.display = 'none';
        } else {
            this.container.style.display = 'block';
        }
        return this.enabled;
    }
}

window.DanmakuEngine = DanmakuEngine;
