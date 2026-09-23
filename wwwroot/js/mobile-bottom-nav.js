/* ==========================================================================
   MOONPHIM - LIQUID GLASS 3D MOBILE BOTTOM NAVIGATION & SPA TRANSITIONS
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
        } else if (path.includes('/user/profile') || path.includes('/user/edit') || path.includes('/user/change-password') || path.includes('/user/payment') || path.includes('tai-khoan')) {
            return 'profile';
        }
        return null;
    }

    function setActiveTab(targetName) {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var navItems = navContainer.querySelectorAll('.liquid-nav-item');
        navItems.forEach(function (el) {
            el.classList.remove('active');
        });

        if (targetName) {
            var activeEl = navContainer.querySelector('[data-nav-target="' + targetName + '"]');
            if (activeEl) {
                activeEl.classList.add('active');
            }
        }
    }

    // Tự động đồng bộ Active tab theo URL
    function syncActiveTabByCurrentUrl() {
        var target = getNavTargetForPath(window.location.pathname);
        setActiveTab(target);
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

        // 1. Kích hoạt hiệu ứng mờ dần (Fade out)
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

            // Đổi body class nếu có (ví dụ home-page)
            if (doc.body.classList.contains('home-page')) {
                document.body.classList.add('home-page');
            } else {
                document.body.classList.remove('home-page');
            }

            // Đợi CSS transition kết thúc một nhịp ngắn
            setTimeout(function () {
                mainElement.innerHTML = newMain.innerHTML;
                window.history.pushState({ path: url, targetTab: targetTab }, '', url);
                window.scrollTo({ top: 0, behavior: 'instant' });

                // Khởi động lại các event listener của trang mới
                reinitializePageScripts();

                // Fade in lại
                mainElement.classList.remove('page-fading');
                isNavigating = false;
            }, 160);
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

        // 4. Lazy Images & Tooltips
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

        // Đồng bộ tab active ban đầu
        syncActiveTabByCurrentUrl();

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
