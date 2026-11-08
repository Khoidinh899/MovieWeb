/**
 * MoonPhim - Shared Announcement Popup Logic (V1 & V2)
 * Handles frequency modes: 'once', 'session', 'interval', 'always'
 * Tracks shows count and interval timestamp.
 */
(function () {
    window.initAnnouncementPopup = function (config) {
        if (!config || !config.version) return;

        var version = config.version;
        var storageKey = 'moonphim_announcement_' + version;
        var sessionKey = 'moonphim_announcement_session_' + version;
        var frequencyMode = (config.frequencyMode || 'once').toLowerCase();
        var intervalHours = Number(config.intervalHours) || 24;
        var maxShows = Number(config.maxShows) || 0;
        var delaySeconds = Math.max(0, Math.min(30, Number(config.delaySeconds) >= 0 ? Number(config.delaySeconds) : 1));

        var stored = { count: 0, last: 0 };
        try {
            var raw = localStorage.getItem(storageKey);
            if (raw) {
                try {
                    var parsed = JSON.parse(raw);
                    if (typeof parsed === 'object' && parsed !== null) {
                        stored.count = Number(parsed.count) || 0;
                        stored.last = Number(parsed.last) || 0;
                    } else if (raw === '1') {
                        stored.count = 1;
                        stored.last = Date.now();
                    }
                } catch (jsonErr) {
                    if (raw === '1') {
                        stored.count = 1;
                        stored.last = Date.now();
                    }
                }
            }
        } catch (e) {
            console.warn('LocalStorage error:', e);
        }

        function hasCookie() {
            try {
                return document.cookie.indexOf('moonphim_announcement_seen_' + version + '=1') !== -1 ||
                       document.cookie.indexOf(storageKey + '=1') !== -1;
            } catch (e) {
                return false;
            }
        }

        function shouldShow() {
            if (maxShows > 0 && stored.count >= maxShows) {
                return false;
            }

            if (frequencyMode === 'once') {
                if (stored.count > 0 || hasCookie()) return false;
                return true;
            }

            if (frequencyMode === 'session') {
                try {
                    if (sessionStorage.getItem(sessionKey) === '1') return false;
                } catch (e) {}
                return true;
            }

            if (frequencyMode === 'interval') {
                if (stored.last <= 0) return true;
                var elapsedMs = Date.now() - stored.last;
                var requiredMs = intervalHours * 3600 * 1000;
                return elapsedMs >= requiredMs;
            }

            if (frequencyMode === 'always') {
                return true;
            }

            return false;
        }

        if (!shouldShow()) {
            return;
        }

        function markShown() {
            stored.count = (stored.count || 0) + 1;
            stored.last = Date.now();

            try {
                localStorage.setItem(storageKey, JSON.stringify(stored));
            } catch (e) {}

            if (frequencyMode === 'session') {
                try {
                    sessionStorage.setItem(sessionKey, '1');
                } catch (e) {}
            }

            if (frequencyMode === 'once') {
                try {
                    document.cookie = storageKey + "=1; max-age=31536000; path=/; samesite=lax";
                } catch (e) {}
            }
        }

        function trigger() {
            setTimeout(function () {
                if (!shouldShow()) return;
                markShown();
                if (typeof config.onShow === 'function') {
                    config.onShow();
                }
            }, delaySeconds * 1000);
        }

        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', trigger);
        } else {
            trigger();
        }
    };
})();
