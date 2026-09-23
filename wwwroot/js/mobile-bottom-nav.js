/* ==========================================================================
   MOONPHIM - LIQUID GLASS 3D MOBILE BOTTOM NAVIGATION & SPA ENGINE
   ========================================================================== */

(function (window, document) {
    'use strict';

    var isNavigating = false;

    function getNavTargetForPath(path) {
        path = (path || window.location.pathname).toLowerCase().trim();
        if (path === '/' || path === '/trang-chu' || path === '/trangchu' || path === '') {
            return 'home';
        } else if (path.includes('phim-bo') || path.includes('/series')) {
            return 'series';
        } else if (path.includes('phim-moi') || path.includes('phim-le') || path.includes('hoat-hinh') || path.includes('/the-loai') || path.includes('/quoc-gia')) {
            return 'new';
        } else if (path.includes('/user/history') || path.includes('/user/favorite') || path.includes('lich-su')) {
            return 'history';
        } else if (path.includes('/user/profile') || path.includes('/user/edit') || path.includes('/user/change-password') || path.includes('/user/payment') || path.includes('/user/notifications') || path.includes('tai-khoan')) {
            return 'profile';
        }
        return null;
    }

    // Cập nhật vị trí viên thuốc trượt kính lỏng (Liquid Sliding Pill)
    function updatePillPosition(targetEl) {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var dock = navContainer.querySelector('.liquid-glass-dock');
        var pill = navContainer.querySelector('.liquid-pill-indicator');
        if (!dock || !pill) return;

        if (!targetEl) {
            pill.classList.remove('active');
            return;
        }

        var dockRect = dock.getBoundingClientRect();
        var itemRect = targetEl.getBoundingClientRect();

        if (itemRect.width === 0) return;

        var leftOffset = itemRect.left - dockRect.left;
        var itemWidth = itemRect.width;

        pill.style.width = itemWidth + 'px';
        pill.style.transform = 'translateX(' + leftOffset + 'px)';
        pill.classList.add('active');
    }

    function setActiveTab(targetName) {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var navItems = navContainer.querySelectorAll('.liquid-nav-item');
        var activeEl = null;

        navItems.forEach(function (el) {
            el.classList.remove('active');
        });

        if (targetName) {
            activeEl = navContainer.querySelector('[data-nav-target="' + targetName + '"]');
            if (activeEl) {
                activeEl.classList.add('active');
            }
        }

        updatePillPosition(activeEl);
    }

    // Tự động đồng bộ Active tab theo URL
    function syncActiveTabByCurrentUrl() {
        var target = getNavTargetForPath(window.location.pathname);
        setActiveTab(target);
    }

    // Nạp đồng bộ CSS & Script còn thiếu từ trang mới sang DOM hiện tại
    function syncAssetsFromNewDoc(doc) {
        // 1. Đồng bộ các thẻ <link rel="stylesheet">
        var newLinks = doc.querySelectorAll('link[rel="stylesheet"]');
        newLinks.forEach(function (link) {
            var href = link.getAttribute('href');
            if (!href) return;
            // Chuẩn hóa so sánh URL
            var exists = Array.prototype.some.call(document.querySelectorAll('link[rel="stylesheet"]'), function (existingLink) {
                return existingLink.getAttribute('href') === href || existingLink.href === link.href;
            });

            if (!exists) {
                var newLinkEl = document.createElement('link');
                newLinkEl.rel = 'stylesheet';
                newLinkEl.href = href;
                document.head.appendChild(newLinkEl);
            }
        });

        // 2. Đồng bộ các thẻ <style> nội tuyến nếu có trong head
        var newStyles = doc.querySelectorAll('head style');
        newStyles.forEach(function (styleEl) {
            var cssText = styleEl.textContent.trim();
            if (!cssText) return;
            var styleExists = Array.prototype.some.call(document.querySelectorAll('head style'), function (existingStyle) {
                return existingStyle.textContent.trim() === cssText;
            });
            if (!styleExists) {
                var newStyle = document.createElement('style');
                newStyle.textContent = cssText;
                document.head.appendChild(newStyle);
            }
        });

        // 3. Nạp các file script đặc thù của trang (như user-history.js, user-favorite.js, profile.js)
        var newScripts = doc.querySelectorAll('script[src]');
        newScripts.forEach(function (script) {
            var src = script.getAttribute('src');
            if (!src) return;
            var scriptExists = Array.prototype.some.call(document.querySelectorAll('script[src]'), function (existingScript) {
                return existingScript.getAttribute('src') === src || existingScript.src === script.src;
            });

            if (!scriptExists) {
                var newScriptEl = document.createElement('script');
                newScriptEl.src = src;
                document.body.appendChild(newScriptEl);
            }
        });
    }

    // Khởi tạo SPA Transition cho Mobile Navigation
    function executeSpaNavigation(url, targetTab) {
        if (isNavigating) return;
        isNavigating = true;

        var mainElement = document.querySelector('main[role="main"]');
        if (!mainElement) {
            window.location.href = url;
            return;
        }

        // 1. Kích hoạt hiệu ứng mờ dần (Fade out) & Di chuyển viên thuốc + Orb lập tức
        mainElement.classList.add('page-fading');
        setActiveTab(targetTab);

        // 2. Fetch trang đích ngầm
        fetch(url, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
        .then(function (response) {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.text();
        })
        .then(function (html) {
            var parser = new DOMParser();
            var doc = parser.parseFromString(html, 'text/html');

            var newMain = doc.querySelector('main[role="main"]');
            if (!newMain) {
                window.location.href = url;
                return;
            }

            // Đổi title
            var newTitle = doc.querySelector('title');
            if (newTitle) {
                document.title = newTitle.textContent;
            }

            // Đồng bộ class body
            document.body.className = doc.body.className;

            // Đồng bộ CSS & Script của trang mới vào DOM
            syncAssetsFromNewDoc(doc);

            // Đợi CSS fade-out kết thúc một nhịp ngắn (150ms)
            setTimeout(function () {
                mainElement.innerHTML = newMain.innerHTML;
                window.history.pushState({ path: url, targetTab: targetTab }, '', url);
                window.scrollTo({ top: 0, behavior: 'instant' });

                // Khởi động lại các event listener của trang mới
                reinitializePageScripts();

                // Fade in lại
                mainElement.classList.remove('page-fading');
                isNavigating = false;
            }, 150);
        })
        .catch(function (err) {
            console.warn('SPA Navigation fallback to normal load:', err);
            window.location.href = url;
        });
    }

    // Khởi chạy lại các sự kiện trang sau khi swap DOM
    function reinitializePageScripts() {
        // 1. User History handlers
        if (typeof attachRemoveHistoryHandlers === 'function') {
            attachRemoveHistoryHandlers();
        }
        if (typeof attachClearAllHandler === 'function') {
            attachClearAllHandler();
        }

        // 2. User Favorite handlers
        if (typeof attachRemoveFavoriteHandlers === 'function') {
            attachRemoveFavoriteHandlers();
        }

        // 3. Filter Sidebar triggers
        if (typeof initFilterSidebar === 'function') {
            initFilterSidebar();
        }

        // 4. Tooltips & Bootstrap components
        if (window.bootstrap && window.bootstrap.Tooltip) {
            var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }
    }

    function initMobileBottomNav() {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var navItems = navContainer.querySelectorAll('.liquid-nav-item');
        if (!navItems.length) return;

        // Đồng bộ tab active ban đầu & viên thuốc
        setTimeout(syncActiveTabByCurrentUrl, 80);

        // Xử lý Click / Touch
        navItems.forEach(function (item) {
            item.addEventListener('click', function (e) {
                var target = this.getAttribute('data-nav-target');
                var href = this.getAttribute('href');
                var isUserLoggedIn = document.body.getAttribute('data-user-logged-in') === 'true';

                // Nếu là nút Profile hoặc History mà user chưa đăng nhập -> Mở modal Auth
                if ((target === 'profile' || target === 'history') && !isUserLoggedIn) {
                    e.preventDefault();
                    if (window.bootstrap && document.getElementById('authModal')) {
                        var authModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('authModal'));
                        authModal.show();
                    } else {
                        window.location.href = href;
                    }
                    return;
                }

                // Nếu click vào tab hiện tại -> cuộn mượt lên đầu trang
                var currentNavTarget = getNavTargetForPath(window.location.pathname);
                if (target === currentNavTarget && window.location.pathname === href) {
                    e.preventDefault();
                    window.scrollTo({ top: 0, behavior: 'smooth' });
                    return;
                }

                // Nếu là chuyển tab thông thường -> Thực thi SPA mượt mà
                if (href && href.startsWith('/')) {
                    e.preventDefault();
                    executeSpaNavigation(href, target);
                }
            });
        });

        // Xử lý nút Back/Forward của trình duyệt
        window.addEventListener('popstate', function (e) {
            syncActiveTabByCurrentUrl();
            if (e.state && e.state.path) {
                executeSpaNavigation(e.state.path, e.state.targetTab || getNavTargetForPath(e.state.path));
            } else {
                window.location.reload();
            }
        });

        // Lắng nghe thay đổi kích thước / xoay màn hình để tính lại vị trí viên thuốc
        window.addEventListener('resize', function () {
            var activeEl = navContainer.querySelector('.liquid-nav-item.active');
            if (activeEl) {
                updatePillPosition(activeEl);
            }
        });

        // Tự động ẩn thanh điều hướng khi xem phim toàn màn hình (Fullscreen)
        var handleFullscreenChange = function () {
            var isFullscreen = !!(document.fullscreenElement || document.webkitFullscreenElement || document.mozFullScreenElement || document.msFullscreenElement);
            if (isFullscreen) {
                document.body.classList.add('is-fullscreen');
                navContainer.classList.add('hide-nav');
            } else {
                document.body.classList.remove('is-fullscreen');
                navContainer.classList.remove('hide-nav');
            }
        };

        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('mozfullscreenchange', handleFullscreenChange);
        document.addEventListener('MSFullscreenChange', handleFullscreenChange);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initMobileBottomNav);
    } else {
        initMobileBottomNav();
    }

})(window, document);
