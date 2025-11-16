// wwwroot/js/notification.js - FIXED WITH AUTH CHECK
(function () {
    'use strict';

    let currentTab = 'payment';
    let isDropdownOpen = false;
    let signalRConnection = null;

    // ========== CACHE NOTIFICATIONS ==========
    let notificationCache = {
        payment: null,
        movie: null
    };

    document.addEventListener('DOMContentLoaded', function () {
        // ✅ KIỂM TRA XEM CÓ NOTIFICATION BELL KHÔNG (CHỈ USER ĐĂNG NHẬP MỚI CÓ)
        const notificationBell = document.querySelector('.notification-trigger');
        
        if (!notificationBell) {
            console.log('ℹ️ [Notification] User not logged in - skipping initialization');
            return; // ❌ DỪNG NGAY NẾU KHÔNG CÓ CHUÔNG (KHÁCH VÃNG LAI)
        }

        // ✅ NẾU CÓ CHUÔNG (USER ĐĂNG NHẬP) -> KHỞI TẠO
        console.log('✅ [Notification] User logged in - initializing...');
        initNotifications();
        initSignalR();
    });

    // ========== SIGNALR INITIALIZATION ==========
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.error('❌ SignalR library not loaded!');
            return;
        }

        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect()
            .build();

        // ========== RECEIVE NOTIFICATION FROM SIGNALR ==========
        signalRConnection.on("ReceiveNotification", function (notificationObject) {
            console.log("✅ [SignalR] Received notification:", notificationObject);

            const notifType = (notificationObject.type || '').toLowerCase();
            const isPaymentNotif = notifType.includes('payment') || notifType.includes('subscription');
            const isMovieNotif = notifType.includes('movie');

            // ========== INVALIDATE CACHE ==========
            if (isPaymentNotif) notificationCache.payment = null;
            if (isMovieNotif) notificationCache.movie = null;

            // ========== CỘNG BADGE NGAY LẬP TỨC ==========
            const mainBadges = document.querySelectorAll('.notification-badge'); 
            const paymentBadge = document.getElementById('paymentBadge');
            const movieBadge = document.getElementById('movieBadge');

            if (mainBadges.length > 0) mainBadges.forEach(incrementBadgeCount);
            
            if (isPaymentNotif && paymentBadge) incrementBadgeCount(paymentBadge);
            if (isMovieNotif && movieBadge) incrementBadgeCount(movieBadge);

            console.log("✅ [Badge] Updated immediately after SignalR");

            // ========== THÊM VÀO DANH SÁCH NẾU DROPDOWN ĐANG MỞ ==========
            if (isDropdownOpen) {
                if ((currentTab === 'payment' && isPaymentNotif) ||
                    (currentTab === 'movie' && isMovieNotif)) {
                    prependNotificationToList(notificationObject);
                }
            }

            // ========== HIỂN THỊ TOAST ==========
            showToast('info', notificationObject.title || 'Bạn có thông báo mới!');

            // ========== SYNC LẠI SỐ BADGE SAU 3 GIÂY ==========
            setTimeout(() => {
                console.log("🔄 [Badge] Syncing with server...");
                updateBadges();
            }, 3000);
        });

        signalRConnection.on("ForceLogout", function (message) {
            console.warn("Bạn đã bị buộc đăng xuất:", message);
            alert(message || "Tài khoản của bạn đã bị khóa hoặc thay đổi quyền. Vui lòng đăng nhập lại.");

            const form = document.createElement('form');
            form.method = 'POST';
            form.action = '/Auth/Logout';

            const token = document.getElementById('RequestVerificationToken').value;
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '__RequestVerificationToken';
            tokenInput.value = token;
            form.appendChild(tokenInput);

            document.body.appendChild(form);
            form.submit();
        });

        signalRConnection.start()
            .then(() => console.log("✅ [SignalR] Connected successfully"))
            .catch(err => console.error("❌ [SignalR] Connection error:", err));

        signalRConnection.onclose(error => {
            console.warn("⚠️ [SignalR] Connection closed", error);
        });
    }

    // ========== INCREMENT BADGE COUNT ==========
    function incrementBadgeCount(badgeElement) {
        if (!badgeElement) return;

        let currentCount = 0;
        if (badgeElement.style.display !== 'none' && badgeElement.textContent) {
            currentCount = parseInt(badgeElement.textContent.replace('+', '')) || 0;
        }

        const newCount = currentCount + 1;
        badgeElement.textContent = newCount > 99 ? '99+' : newCount;

        if (badgeElement.classList.contains('notification-badge')) {
            badgeElement.style.display = 'block';
        } else {
            badgeElement.style.display = 'inline-block';
        }
    }

    // ========== PREPEND NOTIFICATION TO LIST ==========
    function prependNotificationToList(notif) {
        const listContainer = document.getElementById('notificationList');

        if (!listContainer) {
            console.warn('⚠️ notificationList not found');
            return;
        }

        const emptyState = listContainer.querySelector('.empty-state-message');
        if (emptyState) {
            listContainer.innerHTML = '';
        }

        const newItemHtml = createNotificationItemHtml(notif);
        listContainer.insertAdjacentHTML('afterbegin', newItemHtml);

        const newItem = listContainer.firstElementChild;
        if (newItem) {
            attachReadEvent(newItem);
        }
    }

    // ========== CREATE NOTIFICATION HTML ==========
    function createNotificationItemHtml(notif) {
        const id = notif.notificationId ?? notif.NotificationId ?? 0;
        const title = notif.title ?? notif.Title ?? 'Thông báo mới';
        const content = notif.content ?? notif.Content ?? '';
        const type = (notif.type ?? notif.Type ?? 'general').toLowerCase();
        const url = notif.url ?? notif.Url ?? '#';
        const isRead = notif.isRead ?? notif.IsRead ?? false;
        const createdAt = notif.createdAt ?? notif.CreatedAt ?? new Date().toISOString();

        const isPaymentType = type.includes('payment') || type.includes('subscription');
        const icon = isPaymentType ? '<i class="bi bi-credit-card"></i>' : '<i class="bi bi-film"></i>';
        const iconClass = isPaymentType ? 'payment' : 'movie';

        const unreadClass = isRead ? '' : 'unread';
        const timeAgo = formatDateTime(createdAt);

        return `
            <a href="${escapeHtml(url)}"
               class="notification-item ${unreadClass} d-flex"
               data-id="${id}">
                <div class="notification-icon ${iconClass}">
                    ${icon}
                </div>
                <div class="flex-grow-1">
                    <h6 class="mb-1">${escapeHtml(title)}</h6>
                    <p class="mb-1">${escapeHtml(content)}</p>
                    <small class="text-muted">
                        <i class="bi bi-clock me-1"></i>${timeAgo}
                    </small>
                </div>
            </a>
        `;
    }

    // ========== ATTACH READ EVENT ==========
    function attachReadEvent(itemElement) {
        itemElement.addEventListener('click', function (e) {
            e.preventDefault();
            const notifId = parseInt(this.dataset.id);
            const url = this.getAttribute('href');

            if (notifId > 0) {
                markAsRead(notifId, url);
            } else if (url && url !== '#') {
                window.location.href = url;
            }
        });
    }

    // ========== NOTIFICATION UI ==========
    function initNotifications() {
        const bellTriggers = document.querySelectorAll('.notification-trigger');
        const dropdown = document.querySelector('.notification-dropdown');

        if (bellTriggers.length === 0 || !dropdown) {
            console.warn('⚠️ Notification elements not found (triggers or dropdown)');
            return;
        }

        document.body.appendChild(dropdown);
        dropdown.style.position = 'fixed';
        dropdown.style.zIndex = '1050';

        bellTriggers.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                toggleDropdown(e.currentTarget);
            });
        });

        document.addEventListener('click', (e) => {
            let clickedOnTrigger = false;
            bellTriggers.forEach(btn => {
                if (btn.contains(e.target)) {
                    clickedOnTrigger = true;
                }
            });

            if (isDropdownOpen && !dropdown.contains(e.target) && !clickedOnTrigger) {
                closeDropdown();
            }
        });

        const tabButtons = document.querySelectorAll('.nav-link[data-tab]');
        tabButtons.forEach(btn => {
            btn.addEventListener('click', function () {
                currentTab = this.dataset.tab;
                setActiveTab(currentTab);
                loadNotifications();
            });
        });

        const markAllBtn = document.querySelector('.mark-all-read');
        if (markAllBtn) {
            markAllBtn.addEventListener('click', markAllAsRead);
        }

        updateBadges();
        setInterval(updateBadges, 300000);
    }

    function toggleDropdown(anchorBtn) {
        const dropdown = document.querySelector('.notification-dropdown');

        if (isDropdownOpen) {
            closeDropdown();
        } else {
            const rect = anchorBtn.getBoundingClientRect();
            dropdown.style.top = `${rect.bottom + 5}px`;
            dropdown.style.right = `${window.innerWidth - rect.right}px`;
            dropdown.style.left = 'auto';
            if (window.innerWidth < 480) {
                dropdown.style.right = '10px';
                dropdown.style.left = '10px';
            }

            dropdown.style.display = 'block';
            isDropdownOpen = true;

            setActiveTab(currentTab);
            console.log("🔄 [Dropdown] Opening - Prefetching both notification types...");
            prefetchNotifications('payment');
            prefetchNotifications('movie');
            loadNotifications();
        }
    }

    function closeDropdown() {
        const dropdown = document.querySelector('.notification-dropdown');
        dropdown.style.display = 'none';
        isDropdownOpen = false;
    }

    function setActiveTab(tabName) {
        const tabButtons = document.querySelectorAll('.nav-link[data-tab]');
        tabButtons.forEach(b => {
            b.classList.toggle('active', b.dataset.tab === tabName);
        });
    }

    // ========== PREFETCH NOTIFICATIONS (SILENT) ==========
    function prefetchNotifications(type) {
        if (notificationCache[type] !== null) {
            console.log(`✅ [Cache] ${type} notifications already cached`);
            return;
        }

        fetch(`/api/notifications?type=${type}`)
            .then(response => response.ok ? response.json() : Promise.reject('Network error'))
            .then(data => {
                if (data.success && data.data) {
                    notificationCache[type] = data.data;
                    console.log(`✅ [Prefetch] ${type} notifications loaded:`, data.data.length);
                } else {
                    notificationCache[type] = [];
                }
            })
            .catch(error => {
                console.error(`❌ Error prefetching ${type} notifications:`, error);
                notificationCache[type] = [];
            });
    }

    // ========== LOAD NOTIFICATIONS FROM CACHE OR API ==========
    function loadNotifications() {
        const listContainer = document.getElementById('notificationList');

        if (notificationCache[currentTab] !== null) {
            console.log(`✅ [Cache] Loading ${currentTab} from cache`);

            if (notificationCache[currentTab].length > 0) {
                renderNotifications(notificationCache[currentTab]);
            } else {
                showEmptyState(listContainer);
            }
            return;
        }

        listContainer.innerHTML = `
            <div class="text-center py-4">
                <div class="spinner-border spinner-border-sm text-primary"></div>
                <p class="text-muted mt-2 mb-0">Đang tải...</p>
            </div>
        `;

        fetch(`/api/notifications?type=${currentTab}`)
            .then(response => response.ok ? response.json() : Promise.reject('Network error'))
            .then(data => {
                if (data.success && data.data && data.data.length > 0) {
                    console.log('📥 [API] Received notifications:', data.data.map(n => ({
                        id: n.notificationId || n.NotificationId,
                        title: n.title || n.Title,
                        createdAt: n.createdAt || n.CreatedAt
                    })));

                    notificationCache[currentTab] = data.data;
                    renderNotifications(data.data);
                } else {
                    notificationCache[currentTab] = [];
                    showEmptyState(listContainer);
                }
            })
            .catch(error => {
                console.error('❌ Error loading notifications:', error);
                notificationCache[currentTab] = [];
                listContainer.innerHTML = `
                    <div class="text-center py-4 text-danger">
                        <i class="bi bi-exclamation-triangle fs-3 d-block mb-2"></i>
                        <p class="mb-0">Không thể tải thông báo</p>
                    </div>
                `;
            });
    }

    function renderNotifications(notifications) {
        const listContainer = document.getElementById('notificationList');

        const sortedNotifications = [...notifications].sort((a, b) => {
            const dateA = parseUtcDate(a.createdAt || a.CreatedAt);
            const dateB = parseUtcDate(b.createdAt || b.CreatedAt);
            return dateB - dateA;
        });

        listContainer.innerHTML = sortedNotifications.map(createNotificationItemHtml).join('');
        listContainer.querySelectorAll('.notification-item').forEach(attachReadEvent);
    }

    function showEmptyState(container) {
        container.innerHTML = `
            <div class="text-center py-5 text-muted empty-state-message">
                <i class="bi bi-bell-slash fs-1 d-block mb-3" style="opacity: 0.3;"></i>
                <p class="mb-0">Không có thông báo nào</p>
            </div>
        `;
    }

    // ========== UPDATE BADGES ==========
    function updateBadges() {
        fetch('/api/notifications/unread-count')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateBadgeElement('.notification-badge', data.total);
                    updateBadgeElement('#paymentBadge', data.payment);
                    updateBadgeElement('#movieBadge', data.movie);
                }
            })
            .catch(error => console.error('❌ Error updating badges:', error));
    }

    function updateBadgeElement(selector, count) {
        const badges = document.querySelectorAll(selector);
        
        if (badges.length > 0) {
            badges.forEach(badge => {
                if (count > 0) {
                    badge.textContent = count > 99 ? '99+' : count;
                    badge.style.display = badge.classList.contains('notification-badge') ? 'block' : 'inline-block';
                } else {
                    badge.style.display = 'none';
                }
            });
        }
    }

    // ========== MARK AS READ ==========
    function markAsRead(notifId, redirectUrl) {
        fetch(`/api/notifications/${notifId}/mark-read`, { method: 'POST' })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    notificationCache.payment = null;
                    notificationCache.movie = null;

                    updateBadges();
                    if (redirectUrl && redirectUrl !== '#') {
                        window.location.href = redirectUrl;
                    } else {
                        const item = document.querySelector(`.notification-item[data-id="${notifId}"]`);
                        if (item) item.classList.remove('unread');
                    }
                }
            })
            .catch(error => console.error('❌ Error marking as read:', error));
    }

    function markAllAsRead() {
        if (!confirm(`Đánh dấu tất cả thông báo '${currentTab}' là đã đọc?`)) return;

        fetch(`/api/notifications/mark-all-read?type=${currentTab}`, { method: 'POST' })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    notificationCache[currentTab] = null;

                    updateBadges();
                    loadNotifications();
                    showToast('success', 'Đã đánh dấu tất cả là đã đọc');
                }
            })
            .catch(error => {
                console.error('❌ Error marking all as read:', error);
                showToast('error', 'Có lỗi xảy ra');
            });
    }

    // ========== HELPER FUNCTIONS ==========
    function parseUtcDate(dateStr) {
        if (!dateStr) return new Date();

        try {
            let processedDateStr = dateStr.replace(' ', 'T').replace(',', '.');

            const dotIndex = processedDateStr.lastIndexOf('.');
            if (dotIndex > -1) {
                const mainPart = processedDateStr.substring(0, dotIndex);
                const restPart = processedDateStr.substring(dotIndex + 1);

                const fraction = restPart.replace(/[^0-9]/g, '').substring(0, 3);
                const tz = restPart.replace(/[0-9]/g, '');

                processedDateStr = `${mainPart}.${fraction}${tz}`;
            }

            const hasTimezoneRegex = /Z|([+-]\d{2}(:\d{2})?)$/;
            if (!hasTimezoneRegex.test(processedDateStr)) {
                processedDateStr += 'Z';
            }

            const date = new Date(processedDateStr);
            if (isNaN(date.getTime())) {
                console.error(`❌ [Time] Invalid Date: ${dateStr} -> ${processedDateStr}`);
                return new Date();
            }

            return date;

        } catch (e) {
            console.error("Error parsing date:", dateStr, e);
            return new Date();
        }
    }

    function formatDateTime(dateStr) {
        if (!dateStr) return 'N/A';
        try {
            const date = parseUtcDate(dateStr);

            const now = new Date();
            const diff = now - date;
            const seconds = Math.floor(diff / 1000);
            const minutes = Math.floor(seconds / 60);
            const hours = Math.floor(minutes / 60);
            const days = Math.floor(hours / 24);

            if (seconds < 5) return 'Vừa xong';
            if (minutes === 0) return 'Vừa xong';
            if (minutes < 60) return `${minutes} phút trước`;
            if (hours < 24) return `${hours} giờ trước`;
            if (days < 7) return `${days} ngày trước`;
            return date.toLocaleDateString('vi-VN');
        } catch (e) {
            console.error("Error formatting date:", dateStr, e);
            return 'N/A';
        }
    }

    function escapeHtml(text) {
        if (typeof text !== 'string') return '';
        const map = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    function showToast(type, message) {
        const typeClass = type === 'success' ? 'success' : (type === 'info' ? 'info' : 'danger');
        const iconClass = type === 'success' ? 'check-circle' : (type === 'info' ? 'info-circle' : 'exclamation-triangle');

        const toast = document.createElement('div');
        toast.className = `alert alert-${typeClass} d-flex align-items-center toast-notification`;
        toast.innerHTML = `
            <i class="bi bi-${iconClass} me-2 fs-5"></i>
            <span>${escapeHtml(message)}</span>
        `;

        document.body.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(100px)';
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }
})();