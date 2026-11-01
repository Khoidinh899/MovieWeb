(function () {
            if (window.moonphimVignetteInitialized) return;
            window.moonphimVignetteInitialized = true;
            var loadMonetag = function () {
                if (document.querySelector('script[data-zone="11708850"]')) return;
                // Nạp Vignette (Zone 11708850)
                var vig = document.createElement('script');
                vig.dataset.zone = '11708850';
                vig.src = 'https://n6wxm.com/vignette.min.js';
                document.body.appendChild(vig);
            };

            if (document.readyState === 'complete') {
                setTimeout(loadMonetag, 800);
            } else {
                window.addEventListener('load', function () {
                    if ('requestIdleCallback' in window) {
                        requestIdleCallback(loadMonetag, { timeout: 1500 });
                    } else {
                        setTimeout(loadMonetag, 800);
                    }
                });
            }
        })();
