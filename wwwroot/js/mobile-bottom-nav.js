/* ==========================================================================
   MOONPHIM - LIQUID GLASS 3D MOBILE BOTTOM NAVIGATION SCRIPT
   ========================================================================== */

(function () {
    'use strict';

    function initMobileBottomNav() {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var dock = navContainer.querySelector('.liquid-glass-dock');
        var pill = navContainer.querySelector('.liquid-pill-indicator');
        var navItems = navContainer.querySelectorAll('.liquid-nav-item');
        if (!dock || !pill || !navItems.length) return;

        var currentPath = window.location.pathname.toLowerCase();

        // 1. Xác định tab Active dựa vào URL hiện tại
        var activeItem = null;

        navItems.forEach(function (item) {
            var target = item.getAttribute('data-nav-target');
            var href = item.getAttribute('href');

            if (target === 'home' && (currentPath === '/' || currentPath === '/trang-chu' || currentPath === '')) {
                activeItem = item;
            } else if (target === 'new' && (currentPath.includes('phim-moi') || currentPath.includes('phim-le') || currentPath.includes('phim-bo'))) {
                activeItem = item;
            } else if (target === 'search' && (currentPath.includes('tim-kiem') || currentPath.includes('search'))) {
                activeItem = item;
            } else if (target === 'history' && (currentPath.includes('lich-su') || currentPath.includes('yeu-thich') || currentPath.includes('history'))) {
                activeItem = item;
            } else if (target === 'profile' && (currentPath.includes('tai-khoan') || currentPath.includes('profile') || currentPath.includes('user'))) {
                activeItem = item;
            }
        });

        // Mặc định nếu không match trang nào, nếu ở trang chủ thì chọn Home
        if (!activeItem) {
            if (currentPath === '/' || currentPath === '/trang-chu') {
                activeItem = navContainer.querySelector('[data-nav-target="home"]');
            }
        }

        // 2. Cập nhật vị trí viên thuốc kính lỏng (Liquid Pill)
        function updatePillPosition(targetEl) {
            if (!targetEl || targetEl.classList.contains('center-home')) {
                // Nút trang chủ đã có 3D Orb riêng, ẩn viên thuốc dưới nền
                pill.classList.remove('active');
                return;
            }

            var dockRect = dock.getBoundingClientRect();
            var itemRect = targetEl.getBoundingClientRect();

            var leftOffset = itemRect.left - dockRect.left;
            var itemWidth = itemRect.width;

            pill.style.transform = 'translateX(' + leftOffset + 'px)';
            pill.style.width = itemWidth + 'px';
            pill.classList.add('active');
        }

        if (activeItem) {
            activeItem.classList.add('active');
            // Delay nhẹ để layout tính đúng kích thước DOM
            setTimeout(function () {
                updatePillPosition(activeItem);
            }, 100);
        }

        // 3. Xử lý sự kiện chạm/click các tab
        navItems.forEach(function (item) {
            item.addEventListener('click', function (e) {
                // Nếu là nút Profile mà user chưa đăng nhập, kiểm tra modal Auth
                var target = this.getAttribute('data-nav-target');
                var isUserLoggedIn = document.body.getAttribute('data-user-logged-in') === 'true';

                if (target === 'profile' && !isUserLoggedIn) {
                    e.preventDefault();
                    if (window.bootstrap && document.getElementById('authModal')) {
                        var authModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('authModal'));
                        authModal.show();
                    } else {
                        window.location.href = '/tai-khoan';
                    }
                    return;
                }

                navItems.forEach(function (el) { el.classList.remove('active'); });
                this.classList.add('active');
                updatePillPosition(this);
            });
        });

        // 4. Lắng nghe thay đổi kích thước / xoay màn hình
        window.addEventListener('resize', function () {
            var currentActive = navContainer.querySelector('.liquid-nav-item.active');
            if (currentActive) {
                updatePillPosition(currentActive);
            }
        });

        // 5. Tự động ẩn thanh điều hướng khi xem phim toàn màn hình (Fullscreen)
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
})();
