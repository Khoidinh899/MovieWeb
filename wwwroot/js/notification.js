// wwwroot/js/notification.js - FINAL FIXED VERSION (HANDLES ALL TIMEZONES)
(function() {
    'use strict';

    let currentTab = 'payment';
    let isDropdownOpen = false;
    let signalRConnection = null;
    
    // ========== CACHE NOTIFICATIONS ==========
    let notificationCache = {
        payment: null,
        movie: null
    };

    document.addEventListener('DOMContentLoaded', function() {
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
            const mainBadge = document.querySelector('.notification-badge');
            const paymentBadge = document.getElementById('paymentBadge');
            const movieBadge = document.getElementById('movieBadge');

            if (mainBadge) incrementBadgeCount(mainBadge);
            if (isPaymentNotif && paymentBadge) incrementBadgeCount(paymentBadge);
            if (isMovieNotif && movieBadge) incrementBadgeCount(movieBadge);

            console.log("✅ [Badge] Updated immediately after SignalR");

            // ========== THÊM VÀO DANH SÁCH NẾU DROPDOWN ĐANG MỞ ==========
            if (isDropdownOpen) {
                // Chỉ thêm nếu đúng tab
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

        // Xóa empty state nếu có
        const emptyState = listContainer.querySelector('.empty-state-message');
        if (emptyState) {
            listContainer.innerHTML = '';
        }

        // Tạo HTML và thêm vào đầu
        const newItemHtml = createNotificationItemHtml(notif);
        listContainer.insertAdjacentHTML('afterbegin', newItemHtml);

        // Gắn event click
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
        const timeAgo = formatDateTime(createdAt); // <- This will now be correct

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
        itemElement.addEventListener('click', function(e) {
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
        const bellBtn = document.querySelector('.notification-trigger');
        const dropdown = document.querySelector('.notification-dropdown');

        if (!bellBtn || !dropdown) {
            console.warn('⚠️ Notification elements not found');
            return;
        }

        bellBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleDropdown();
        });

        document.addEventListener('click', (e) => {
            if (isDropdownOpen && !dropdown.contains(e.target) && !bellBtn.contains(e.target)) {
                closeDropdown();
            }
        });

        // Tab buttons
        const tabButtons = document.querySelectorAll('.nav-link[data-tab]');
        tabButtons.forEach(btn => {
            btn.addEventListener('click', function() {
                currentTab = this.dataset.tab;
                setActiveTab(currentTab);
                loadNotifications(); // Load lại khi switch tab
            });
        });

        // Mark all read
        const markAllBtn = document.querySelector('.mark-all-read');
        if (markAllBtn) {
            markAllBtn.addEventListener('click', markAllAsRead);
        }

        updateBadges();
        setInterval(updateBadges, 300000); // Poll mỗi 5 phút
    }

    function toggleDropdown() {
        const dropdown = document.querySelector('.notification-dropdown');

        if (isDropdownOpen) {
            closeDropdown();
        } else {
            dropdown.style.display = 'block';
            isDropdownOpen = true;
            setActiveTab(currentTab);

            // ========== FIX: PREFETCH CẢ 2 LOẠI NOTIFICATION ==========
            console.log("🔄 [Dropdown] Opening - Prefetching both notification types...");
            
            // Prefetch cả 2 loại (không đợi)
            prefetchNotifications('payment');
            prefetchNotifications('movie');
            
            // Load tab hiện tại
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
        // Nếu đã có cache thì skip
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
        
        // Nếu có cache, dùng luôn
        if (notificationCache[currentTab] !== null) {
            console.log(`✅ [Cache] Loading ${currentTab} from cache`);
            
            if (notificationCache[currentTab].length > 0) {
                renderNotifications(notificationCache[currentTab]);
            } else {
                showEmptyState(listContainer);
            }
            return;
        }

        // Không có cache, show loading và fetch
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
                    // ✅ DEBUG: Log thứ tự nhận được
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
        
        // ✅ Sort lại ở frontend để đảm bảo mới nhất trước
        const sortedNotifications = [...notifications].sort((a, b) => {
            // Sử dụng hàm formatDateTime để lấy Date object đã được xử lý timezone
            const dateA = parseUtcDate(a.createdAt || a.CreatedAt);
            const dateB = parseUtcDate(b.createdAt || b.CreatedAt);
            return dateB - dateA; // Mới nhất trước
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
        const badge = document.querySelector(selector);
        if (badge) {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.style.display = selector === '.notification-badge' ? 'block' : 'inline-block';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    // ========== MARK AS READ ==========
    function markAsRead(notifId, redirectUrl) {
        fetch(`/api/notifications/${notifId}/mark-read`, { method: 'POST' })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    // Invalidate cache
                    notificationCache.payment = null;
                    notificationCache.movie = null;
                    
                    updateBadges();
                    if (redirectUrl && redirectUrl !== '#') {
                        window.location.href = redirectUrl;
                    } else {
                        const item = document.querySelector(`.notification-item[data-id="${notifId}"]`);
                        if(item) item.classList.remove('unread');
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
                    // Invalidate cache
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

    /**
     * ✅ [FIXED v2]
     * Helper này xử lý string thời gian từ C#
     * 1. Chuẩn hóa string (thay ' ' -> 'T', ',' -> '.')
     * 2. Cắt bớt mili-giây (nếu .NET gửi 7 số, JS chỉ nhận 3)
     * 3. Thêm 'Z' (UTC) nếu string không có thông tin timezone
     */
    function parseUtcDate(dateStr) {
        if (!dateStr) return new Date(); // Trả về now nếu rỗng

        try {
            // 1. Chuẩn hóa string: thay ' ' (space) = 'T', thay ',' (comma) = '.'
            //    (Phòng trường hợp C# serialize khác)
            let processedDateStr = dateStr.replace(' ', 'T').replace(',', '.');

            // 2. [FIXED v2] Xử lý mili-giây (logic mới, đơn giản hơn)
            const dotIndex = processedDateStr.lastIndexOf('.');
            if (dotIndex > -1) {
                const mainPart = processedDateStr.substring(0, dotIndex); // vd: ...T06:03:00
                const restPart = processedDateStr.substring(dotIndex + 1); // vd: 1234567Z hoặc 1234567

                // Lấy 3 số mili-giây đầu tiên
                const fraction = restPart.replace(/[^0-9]/g, '').substring(0, 3); // "123"
                // Lấy phần timezone (nếu có)
                const tz = restPart.replace(/[0-9]/g, ''); // "Z" hoặc "" hoặc "+07:00"

                // Ghép lại
                processedDateStr = `${mainPart}.${fraction}${tz}`; // vd: ...T06:03:00.123Z
            }

            // 3. Thêm 'Z' (UTC) nếu string không có thông tin timezone
            // (Áp dụng cho các string `DateTime.Now` mới từ server UTC)
            const hasTimezoneRegex = /Z|([+-]\d{2}(:\d{2})?)$/;
            if (!hasTimezoneRegex.test(processedDateStr)) {
                processedDateStr += 'Z';
            }

            const date = new Date(processedDateStr);
            if (isNaN(date.getTime())) {
                // Nếu parse lỗi (rất hiếm), trả về 'Vừa xong'
                console.error(`❌ [Time] Invalid Date: ${dateStr} -> ${processedDateStr}`);
                return new Date(); 
            }
            
            // Log để debug
            // console.log(`✅ [Time] Parsed: ${dateStr} -> ${processedDateStr} -> ${date.toISOString()}`);
            return date;

        } catch (e) {
            console.error("Error parsing date:", dateStr, e);
            return new Date(); // Lỗi -> fallback về 'Vừa xong'
        }
    }


    /**
     * Hàm này chỉ format, việc tính toán date đã dời qua parseUtcDate
     */
    function formatDateTime(dateStr) {
        if (!dateStr) return 'N/A';
        try {
            // Sử dụng helper mới để parse
            const date = parseUtcDate(dateStr);
            
            const now = new Date();
            const diff = now - date; // Đây là diff mili-giây chính xác
            const seconds = Math.floor(diff / 1000);
            const minutes = Math.floor(seconds / 60);
            const hours = Math.floor(minutes / 60);
            const days = Math.floor(hours / 24);

            // Xử lý trường hợp thời gian tương lai (do server/client lệch nhau < 5s)
            if (seconds < 5) return 'Vừa xong'; 
            if (minutes === 0) return 'Vừa xong'; // Fix cho 0 phút trước
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