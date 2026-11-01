(function () {
                const STORAGE_KEY = 'moonphim_pop_done';
                const COOLDOWN = 2 * 60 * 60 * 1000;
                const ZONE_ID = '11708858';

                const isAppContext = () => {
                    const userAgent = navigator.userAgent || '';
                    const isInstalledPwa = navigator.standalone === true ||
                        window.matchMedia?.('(display-mode: standalone)').matches === true ||
                        window.matchMedia?.('(display-mode: minimal-ui)').matches === true;
                    const isAndroidWebView = /Android/i.test(userAgent) && /\bwv\b/i.test(userAgent);
                    const isNativeApp = /\bMoonPhimApp\b/i.test(userAgent) ||
                        (document.referrer || '').startsWith('android-app://');
                    return isInstalledPwa || isAndroidWebView || isNativeApp;
                };

                // Disable website OnClick in installed apps; ordinary browser tabs keep it.
                if (isAppContext()) return;
                // Shared across SPA replays; a per-initializer flag cannot prevent duplicates.
                const state = window.moonphimOnclickState ||= { pending: false, loaded: false };

                const isCoolingDown = () => {
                    const hasCookie = document.cookie.split(';').some(cookie =>
                        cookie.trim().startsWith(STORAGE_KEY + '='));
                    if (hasCookie) return true;
                    try {
                        const lastLoaded = Number(localStorage.getItem(STORAGE_KEY));
                        return Number.isFinite(lastLoaded) && lastLoaded > 0 &&
                            Date.now() - lastLoaded < COOLDOWN;
                    } catch {
                        return false; // Safari storage restrictions: rely on the cookie/global state.
                    }
                };

                if (state.pending || state.loaded ||
                    document.querySelector('script[data-zone="' + ZONE_ID + '"]') || isCoolingDown()) {
                    return;
                }

                const triggerPopunder = () => {
                    window.removeEventListener('click', triggerPopunder);
                    state.pending = false;
                    // Another tab can start cooldown after this listener was registered.
                    if (isAppContext() || state.loaded || isCoolingDown()) return;
                    state.loaded = true;

                    try { localStorage.setItem(STORAGE_KEY, String(Date.now())); } catch {}
                    document.cookie = STORAGE_KEY + '=1; max-age=7200; path=/; samesite=lax';

                    if (navigator.sendBeacon) {
                        navigator.sendBeacon('/api/ads/record-popunder');
                    } else {
                        fetch('/api/ads/record-popunder', { method: 'POST', keepalive: true }).catch(() => {});
                    }

                    const script = document.createElement('script');
                    script.dataset.zone = ZONE_ID;
                    script.src = 'https://al5sm.com/tag.min.js';
                    (document.body || document.documentElement).appendChild(script);
                };

                state.pending = true;
                window.addEventListener('click', triggerPopunder, { once: true, passive: true });
            })();
