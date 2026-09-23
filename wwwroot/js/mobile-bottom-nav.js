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

        var currentPath = window.location.pathname.toLowerCase().trim();

        // 1. Xác định tab Active dựa vào URL hiện tại
        var activeItem = null;

        // Xóa class active cũ trên tất cả items
        navItems.forEach(function (el) { el.classList.remove('active'); });

        if (currentPath === '/' || currentPath === '/trang-chu' || currentPath === '/trangchu' || currentPath === '') {
            activeItem = navContainer.querySelector('[data-nav-target="home"]');
        } else if (currentPath.includes('phim-bo') || currentPath.includes('/series')) {
            activeItem = navContainer.querySelector('[data-nav-target="series"]');
        } else if (currentPath.includes('phim-moi') || currentPath.includes('phim-le') || currentPath.includes('hoat-hinh') || currentPath.includes('/the-loai') || currentPath.includes('/quoc-gia')) {
            activeItem = navContainer.querySelector('[data-nav-target="new"]');
        } else if (currentPath.includes('/user/history') || currentPath.includes('/user/favorite') || currentPath.includes('lich-su')) {
            activeItem = navContainer.querySelector('[data-nav-target="history"]');
        } else if (currentPath.includes('/user/profile') || currentPath.includes('/user/edit') || currentPath.includes('/user/change-password') || currentPath.includes('/user/payment') || currentPath.includes('tai-khoan')) {
            activeItem = navContainer.querySelector('[data-nav-target="profile"]');
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
        } else {
            pill.classList.remove('active');
        }

        // 3. Xử lý sự kiện chạm/click các tab
        navItems.forEach(function (item) {
            item.addEventListener('click', function (e) {
                var target = this.getAttribute('data-nav-target');
                var isUserLoggedIn = document.body.getAttribute('data-user-logged-in') === 'true';

                // Nếu là nút Profile hoặc History mà user chưa đăng nhập -> Mở modal Auth
                if ((target === 'profile' || target === 'history') && !isUserLoggedIn) {
                    e.preventDefault();
                    if (window.bootstrap && document.getElementById('authModal')) {
                        var authModal = bootstrap.Modal.getOrCreateInstance(document.getElementById('authModal'));
                        authModal.show();
                    } else {
                        window.location.href = target === 'profile' ? '/user/profile' : '/user/history';
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
