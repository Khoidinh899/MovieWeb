/* ==========================================================================
   MOONPHIM - LIQUID GLASS 3D MOBILE BOTTOM NAVIGATION & FULL SPA ENGINE
   ========================================================================== */

(function (window, document) {
    'use strict';

    var isNavigating = false;
    var TAB_ORDER = ['new', 'watching', 'home', 'history', 'profile'];

    function getNavTargetForPath(path) {
        path = (path || window.location.pathname).toLowerCase().trim();
        if (path === '/' || path === '/trang-chu' || path === '/trangchu' || path === '') {
            return 'home';
        } else if (path.startsWith('/phim/')) {
            return 'watching';
        } else if (path.includes('phim-moi') || path.includes('phim-le') || path.includes('phim-bo') || path.includes('hoat-hinh') || path.includes('/the-loai') || path.includes('/quoc-gia')) {
            return 'new';
        } else if (path.includes('/user/history') || path.includes('/user/favorite') || path.includes('lich-su')) {
            return 'history';
        } else if (path.includes('/user/profile') || path.includes('/user/edit') || path.includes('/user/change-password') || path.includes('/user/payment') || path.includes('/user/notifications') || path.includes('tai-khoan')) {
            return 'profile';
        }
        return null;
    }

    // Cập nhật vị trí Quả cầu Kính Lỏng 3D trượt dạng giọt nước (Gliding Liquid Orb)
    function updateSlidingOrbPosition(targetEl) {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var dock = navContainer.querySelector('.liquid-glass-dock');
        var orb = navContainer.querySelector('#liquidSlidingOrb');
        var orbIcon = navContainer.querySelector('#liquidOrbIcon');
        if (!dock || !orb) return;

        if (!targetEl) {
            orb.classList.remove('active');
            return;
        }

        var dockRect = dock.getBoundingClientRect();
        var itemRect = targetEl.getBoundingClientRect();

        if (itemRect.width === 0) return;

        // Tính vị trí tâm chính xác của nút bấm tương ứng
        var itemCenterX = itemRect.left - dockRect.left + (itemRect.width / 2);
        var orbRadius = 24; // Bán kính quả cầu 48px
        var targetX = itemCenterX - orbRadius;

        orb.style.transform = 'translate3d(' + targetX + 'px, 0, 0)';
        orb.classList.add('active');

        // Cập nhật icon bên trong quả cầu
        var targetIcon = targetEl.getAttribute('data-icon');
        if (targetIcon && orbIcon) {
            orbIcon.className = 'liquid-orb-icon bi ' + targetIcon;
        }
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

        updateSlidingOrbPosition(activeEl);
    }

    // Tự động đồng bộ Active tab theo URL hiện tại
    function syncActiveTabByCurrentUrl() {
        var target = getNavTargetForPath(window.location.pathname);
        setActiveTab(target);
    }

    // Nạp đồng bộ CSS & Script còn thiếu từ trang mới sang DOM hiện tại (trả về Promise)
    function syncAssetsFromNewDoc(doc) {
        // 1. Đồng bộ các thẻ <link rel="stylesheet">
        var newLinks = doc.querySelectorAll('link[rel="stylesheet"]');
        newLinks.forEach(function (link) {
            var href = link.getAttribute('href');
            if (!href) return;
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

        // 3. Nạp và chờ các file script đặc thù của trang (như HLS.js, video-ads-handler.js, favorite-handler.js)
        var newScripts = doc.querySelectorAll('script[src]');
        var scriptPromises = [];

        newScripts.forEach(function (script) {
            var src = script.getAttribute('src');
            if (!src) return;

            // Bỏ qua các script layout dùng chung đã nạp sẵn
            if (src.includes('bootstrap') || src.includes('site.js') || src.includes('signalr') ||
                src.includes('mobile-bottom-nav.js') || src.includes('moon-dialog.js') ||
                src.includes('notification.js') || src.includes('chatbot.js')) {
                return;
            }

            var scriptExists = Array.prototype.some.call(document.querySelectorAll('script[src]'), function (existingScript) {
                return existingScript.getAttribute('src') === src || existingScript.src === script.src;
            });

            if (!scriptExists) {
                var p = new Promise(function (resolve) {
                    var newScriptEl = document.createElement('script');
                    newScriptEl.src = src;
                    newScriptEl.onload = function () { resolve(); };
                    newScriptEl.onerror = function () { resolve(); };
                    document.body.appendChild(newScriptEl);
                });
                scriptPromises.push(p);
            }
        });

        return Promise.all(scriptPromises);
    }

    // Khởi tạo SPA Transition cho Mobile Navigation
    function executeSpaNavigation(url, targetTab, isPopState) {
        if (isNavigating) return;
        isNavigating = true;

        var mainElement = document.querySelector('main[role="main"]');
        if (!mainElement) {
            window.location.href = url;
            return;
        }

        // 1. Kích hoạt hiệu ứng mờ dần (Fade out) & Quả cầu trượt lướt tức thì
        mainElement.classList.add('page-fading');
        setActiveTab(targetTab || getNavTargetForPath(url));

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

            // Đồng bộ CSS & Script của trang mới vào DOM rồi mới thực thi
            syncAssetsFromNewDoc(doc).then(function () {
                setTimeout(function () {
                    mainElement.innerHTML = newMain.innerHTML;

                    // Thực thi TẤT CẢ các đoạn script trang mới (trong newMain và trong doc.body / @section Scripts)
                    var scriptsToRun = [];

                    // 1. Script nội tuyến trong newMain
                    newMain.querySelectorAll('script:not([src])').forEach(function (s) {
                        if (s.textContent.trim()) {
                            scriptsToRun.push(s.textContent);
                        }
                    });

                    // 2. Script nội tuyến trong doc.body (như @section Scripts chứa initMovieDetailPage)
                    doc.querySelectorAll('body script:not([src])').forEach(function (s) {
                        var text = s.textContent.trim();
                        if (text && !scriptsToRun.includes(text)) {
                            // Bỏ qua inline script antiforgery nếu có
                            if (!text.includes('RequestVerificationToken') && !text.includes('RequestVerificationToken')) {
                                scriptsToRun.push(text);
                            }
                        }
                    });

                    // Thực thi lần lượt các inline script
                    scriptsToRun.forEach(function (code) {
                        try {
                            var runner = new Function(code);
                            runner();
                        } catch (e) {
                            console.warn('SPA inline script execution error:', e);
                        }
                    });

                    if (!isPopState) {
                        window.history.pushState({ path: url, targetTab: targetTab }, '', url);
                    }

                    window.scrollTo({ top: 0, behavior: 'instant' });

                    // Khởi động lại các event listener của trang mới
                    reinitializePageScripts();

                    // Fade in lại
                    mainElement.classList.remove('page-fading');
                    isNavigating = false;
                }, 80);
            });
        })
        .catch(function (err) {
            console.warn('SPA Navigation fallback to normal load:', err);
            mainElement.classList.remove('page-fading');
            isNavigating = false;
            window.location.href = url;
        });
    }

    // Khởi chạy lại các sự kiện trang sau khi swap DOM
    function reinitializePageScripts() {
        // 1. Chi tiết phim (HLS player, episodes, comments)
        if (typeof window.initMovieDetailPage === 'function') {
            try {
                window.initMovieDetailPage();
            } catch (e) {
                console.warn('Movie detail init error:', e);
            }
        }

        // 2. Khởi tạo lại Bootstrap Carousel Banner Trang Chủ
        if (window.bootstrap && window.bootstrap.Carousel && document.getElementById('movieCarousel')) {
            try {
                var carouselEl = document.getElementById('movieCarousel');
                var existingInstance = bootstrap.Carousel.getInstance(carouselEl);
                if (existingInstance) {
                    existingInstance.dispose();
                }
                new bootstrap.Carousel(carouselEl, {
                    interval: 4000,
                    ride: 'carousel',
                    wrap: true
                });
            } catch (e) {
                console.warn('Carousel init error:', e);
            }
        }

        // 3. User History & Favorite handlers
        if (typeof attachRemoveHistoryHandlers === 'function') {
            attachRemoveHistoryHandlers();
        }
        if (typeof attachClearAllHandler === 'function') {
            attachClearAllHandler();
        }
        if (typeof attachRemoveFavoriteHandlers === 'function') {
            attachRemoveFavoriteHandlers();
        }

        // 4. Gắn chặn click các thẻ phim để đi qua SPA
        attachMovieCardSpaInterceptors();

        // 5. Tooltips & Bootstrap components
        if (window.bootstrap && window.bootstrap.Tooltip) {
            var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }
    }

    // Chuyển hướng các thẻ phim sang SPA mượt mà trên mobile
    function attachMovieCardSpaInterceptors() {
        if (window.innerWidth > 768) return;

        var movieLinks = document.querySelectorAll('a[href^="/phim/"]');
        movieLinks.forEach(function (link) {
            if (link.dataset.spaBound === 'true') return;
            link.dataset.spaBound = 'true';

            link.addEventListener('click', function (e) {
                var href = this.getAttribute('href');
                if (href && href.startsWith('/phim/')) {
                    try {
                        localStorage.setItem('moon_current_watching', JSON.stringify({
                            url: href,
                            updatedAt: Date.now()
                        }));
                    } catch (err) {}
                    e.preventDefault();
                    executeSpaNavigation(href, 'watching');
                }
            });
        });
    }

    // Hiển thị hộp thoại khi chưa có phim đang xem
    function showNoWatchingDialog() {
        if (window.MoonDialog) {
            window.MoonDialog.confirm({
                title: 'Chưa có phim đang xem ✨',
                message: 'Bạn chưa xem bộ phim nào nè! Hãy cùng khám phá kho phim bom tấn vietsub cực hay tại MoonPhim ngay nhé 🍿',
                confirmText: 'Khám Phá Phim Mới',
                cancelText: 'Về Trang Chủ',
                type: 'primary',
                icon: 'info',
                iconClass: 'bi-film'
            }).then(function (isExplore) {
                if (isExplore) {
                    executeSpaNavigation('/the-loai/phim-moi-cap-nhat', 'new');
                } else {
                    executeSpaNavigation('/trang-chu', 'home');
                }
            });
        } else {
            alert('Bạn chưa xem bộ phim nào nè! Hãy cùng khám phá kho phim tại MoonPhim nhé 🍿');
            executeSpaNavigation('/the-loai/phim-moi-cap-nhat', 'new');
        }
    }

    // Xử lý khi bấm nút "Đang Xem" (Now Playing / Quick Resume)
    function handleNowPlayingClick(e) {
        if (e) e.preventDefault();

        // 1. Nếu người dùng đang ở ngay trang chi tiết / xem phim -> cuộn mượt đến player
        if (window.location.pathname.startsWith('/phim/')) {
            var playerEl = document.getElementById('videoContainer') || document.querySelector('.detail-header') || document.body;
            playerEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
            setActiveTab('watching');
            return;
        }

        // 2. Lấy phim gần nhất từ bộ nhớ máy
        var lastWatchedRaw = localStorage.getItem('moon_current_watching');
        var lastWatched = null;
        try {
            if (lastWatchedRaw) lastWatched = JSON.parse(lastWatchedRaw);
        } catch (err) {}

        if (lastWatched && lastWatched.url && lastWatched.url.startsWith('/phim/')) {
            // Có phim đang xem -> Chuyển thẳng tới trang chi tiết phim đó
            executeSpaNavigation(lastWatched.url, 'watching');
            return;
        }

        // 3. Nếu bộ nhớ máy chưa có -> Truy vấn API lịch sử xem gần nhất từ máy chủ
        fetch('/api/watch-history?page=1&pageSize=1', {
            headers: { 'Accept': 'application/json' }
        })
        .then(function (res) {
            if (!res.ok) throw new Error('History fetch error');
            return res.json();
        })
        .then(function (data) {
            var latestItem = null;
            if (data && data.items && data.items.length > 0) {
                latestItem = data.items[0];
            } else if (Array.isArray(data) && data.length > 0) {
                latestItem = data[0];
            }

            if (latestItem && latestItem.slug) {
                var targetUrl = '/phim/' + latestItem.slug;
                try {
                    localStorage.setItem('moon_current_watching', JSON.stringify({
                        url: targetUrl,
                        slug: latestItem.slug,
                        name: latestItem.name || '',
                        updatedAt: Date.now()
                    }));
                } catch (err) {}
                executeSpaNavigation(targetUrl, 'watching');
            } else {
                showNoWatchingDialog();
            }
        })
        .catch(function () {
            showNoWatchingDialog();
        });
    }

    // ==========================================================================
    // 🚀 DUAL GESTURE ENGINE (HỆ THỐNG CỬ CHỈ KÉP THÔNG MINH)
    // ==========================================================================
    function initDualGestureEngine() {
        if (window.innerWidth > 768) return;

        // 1. Khởi tạo phần tử thị giác cho Cử chỉ vuốt mép (Edge Gesture Indicator)
        var edgeIndicator = document.getElementById('moonEdgeGestureIndicator');
        if (!edgeIndicator) {
            edgeIndicator = document.createElement('div');
            edgeIndicator.id = 'moonEdgeGestureIndicator';
            edgeIndicator.className = 'moon-edge-gesture-indicator';
            edgeIndicator.innerHTML = '<i class="bi bi-chevron-left" id="moonEdgeGestureIcon"></i>';
            document.body.appendChild(edgeIndicator);
        }
        var edgeIcon = document.getElementById('moonEdgeGestureIcon');

        var edgeStartX = 0;
        var edgeStartY = 0;
        var edgeMode = null; // 'left' (Back) | 'right' (Forward)
        var isEdgeGestureActive = false;

        document.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) return;
            var touch = e.touches[0];
            var x = touch.clientX;
            var y = touch.clientY;

            var targetTag = e.target.tagName.toLowerCase();
            if (targetTag === 'video' || targetTag === 'iframe' || e.target.closest('#mobileBottomNav')) return;

            var screenWidth = window.innerWidth;
            var EDGE_THRESHOLD = 45;

            if (x <= EDGE_THRESHOLD) {
                edgeStartX = x;
                edgeStartY = y;
                edgeMode = 'left';
                isEdgeGestureActive = true;
            } else if (x >= screenWidth - EDGE_THRESHOLD) {
                edgeStartX = x;
                edgeStartY = y;
                edgeMode = 'right';
                isEdgeGestureActive = true;
            } else {
                isEdgeGestureActive = false;
                edgeMode = null;
            }
        }, { passive: true });

        document.addEventListener('touchmove', function (e) {
            if (!isEdgeGestureActive || !edgeMode) return;
            var touch = e.touches[0];
            var deltaX = touch.clientX - edgeStartX;
            var deltaY = touch.clientY - edgeStartY;

            if (Math.abs(deltaX) > Math.abs(deltaY) * 1.3 && Math.abs(deltaX) > 20) {
                if (edgeMode === 'left' && deltaX > 0) {
                    edgeIndicator.className = 'moon-edge-gesture-indicator left visible' + (deltaX > 65 ? ' triggered' : '');
                    if (edgeIcon) edgeIcon.className = 'bi bi-chevron-left';
                } else if (edgeMode === 'right' && deltaX < 0) {
                    edgeIndicator.className = 'moon-edge-gesture-indicator right visible' + (Math.abs(deltaX) > 65 ? ' triggered' : '');
                    if (edgeIcon) edgeIcon.className = 'bi bi-chevron-right';
                }
            }
        }, { passive: true });

        document.addEventListener('touchend', function (e) {
            if (!isEdgeGestureActive || !edgeMode) return;
            var touch = e.changedTouches[0];
            var deltaX = touch.clientX - edgeStartX;
            var deltaY = touch.clientY - edgeStartY;

            if (Math.abs(deltaX) > Math.abs(deltaY) * 1.3 && Math.abs(deltaX) > 65) {
                if (edgeMode === 'left' && deltaX > 65) {
                    window.history.back();
                } else if (edgeMode === 'right' && deltaX < -65) {
                    window.history.forward();
                }
            }

            edgeIndicator.classList.remove('visible', 'triggered');
            isEdgeGestureActive = false;
            edgeMode = null;
        }, { passive: true });

        // 2. CỬ CHỈ QUẸT TRÊN THANH DOCK CHÂN TRANG (DOCK SWIPE NAVIGATION)
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;
        var dockEl = navContainer.querySelector('.liquid-glass-dock');
        if (!dockEl) return;

        var dockTouchStartX = 0;
        var dockTouchStartY = 0;

        dockEl.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) return;
            dockTouchStartX = e.touches[0].clientX;
            dockTouchStartY = e.touches[0].clientY;
        }, { passive: true });

        dockEl.addEventListener('touchend', function (e) {
            if (e.changedTouches.length !== 1) return;
            var touch = e.changedTouches[0];
            var deltaX = touch.clientX - dockTouchStartX;
            var deltaY = touch.clientY - dockTouchStartY;

            if (Math.abs(deltaX) > 35 && Math.abs(deltaX) > Math.abs(deltaY) * 1.3) {
                var currentTarget = getNavTargetForPath(window.location.pathname);
                var currentIndex = TAB_ORDER.indexOf(currentTarget);
                if (currentIndex === -1) currentIndex = 2; // Mặc định ở Trang Chủ

                var nextIndex = currentIndex;
                if (deltaX < -35) {
                    nextIndex = Math.min(TAB_ORDER.length - 1, currentIndex + 1);
                } else if (deltaX > 35) {
                    nextIndex = Math.max(0, currentIndex - 1);
                }

                if (nextIndex !== currentIndex) {
                    var nextTabName = TAB_ORDER[nextIndex];
                    var nextTabItem = navContainer.querySelector('[data-nav-target="' + nextTabName + '"]');
                    if (nextTabItem) {
                        nextTabItem.click();
                    }
                }
            }
        }, { passive: true });
    }

    function initMobileBottomNav() {
        var navContainer = document.getElementById('mobileBottomNav');
        if (!navContainer) return;

        var navItems = navContainer.querySelectorAll('.liquid-nav-item');
        if (!navItems.length) return;

        // Đồng bộ tab active ban đầu & vị trí quả cầu
        setTimeout(syncActiveTabByCurrentUrl, 80);

        // Khởi chạy Hệ thống Cử chỉ kép thông minh
        initDualGestureEngine();

        // Gắn chặn click các thẻ phim để vào phim qua SPA ngầm
        attachMovieCardSpaInterceptors();

        // Xử lý Click / Touch từng tab
        navItems.forEach(function (item) {
            item.addEventListener('click', function (e) {
                var target = this.getAttribute('data-nav-target');
                var href = this.getAttribute('href');
                var isUserLoggedIn = document.body.getAttribute('data-user-logged-in') === 'true';

                // Nút ĐANG XEM (Now Playing)
                if (target === 'watching') {
                    handleNowPlayingClick(e);
                    return;
                }

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

        // Xử lý nút Back/Forward của trình duyệt (PopState) HOÀN TOÀN BẰNG SPA
        window.addEventListener('popstate', function (e) {
            var currentPath = window.location.pathname + window.location.search;
            var targetTab = (e.state && e.state.targetTab) ? e.state.targetTab : getNavTargetForPath(window.location.pathname);
            executeSpaNavigation(currentPath, targetTab, true);
        });

        // Lắng nghe thay đổi kích thước / xoay màn hình để định vị lại quả cầu
        window.addEventListener('resize', function () {
            var activeEl = navContainer.querySelector('.liquid-nav-item.active');
            if (activeEl) {
                updateSlidingOrbPosition(activeEl);
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
