// wwwroot/js/notification.js
(function() {
    'use strict';

    let currentTab = 'payment';
    let isDropdownOpen = false;

    // Khởi tạo khi DOM ready
    document.addEventListener('DOMContentLoaded', function() {
        initNotifications();
    });

    function initNotifications() {
        const bellBtn = document.querySelector('.notification-trigger');
        const dropdown = document.querySelector('.notification-dropdown');

        if (!bellBtn || !dropdown) {
            console.warn('Notification elements not found');
            return;
        }

        // Toggle dropdown khi click bell
        bellBtn.addEventListener('click', function(e) {
            e.stopPropagation();
            toggleDropdown();
        });

        // Đóng dropdown khi click bên ngoài
        document.addEventListener('click', function(e) {
            if (isDropdownOpen && 
                !dropdown.contains(e.target) && 
                !bellBtn.contains(e.target)) {
                closeDropdown();
            }
        });

        // Tab switching
        const tabButtons = document.querySelectorAll('.nav-link[data-tab]');
        tabButtons.forEach(btn => {
            btn.addEventListener('click', function() {
                currentTab = this.dataset.tab;
                
                // Update active tab
                tabButtons.forEach(b => b.classList.remove('active'));
                this.classList.add('active');
                
                // Load notifications cho tab này
                loadNotifications();
            });
        });

        // Mark all as read
        const markAllBtn = document.querySelector('.mark-all-read');
        if (markAllBtn) {
            markAllBtn.addEventListener('click', function() {
                markAllAsRead();
            });
        }

        // Load ban đầu
        updateBadges();
        
        // Auto refresh mỗi 30 giây
        setInterval(updateBadges, 30000);
    }

    function toggleDropdown() {
        const dropdown = document.querySelector('.notification-dropdown');
        
        if (isDropdownOpen) {
            closeDropdown();
        } else {
            dropdown.style.display = 'block';
            isDropdownOpen = true;
            loadNotifications();
        }
    }

    function closeDropdown() {
        const dropdown = document.querySelector('.notification-dropdown');
        dropdown.style.display = 'none';
        isDropdownOpen = false;
    }

    // Lấy danh sách notifications
    function loadNotifications() {
        const listContainer = document.getElementById('notificationList');
        
        // Show loading
        listContainer.innerHTML = `
            <div class="text-center py-4">
                <div class="spinner-border spinner-border-sm text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
                <p class="text-muted mt-2 mb-0">Đang tải...</p>
            </div>
        `;

        fetch(`/api/notifications?type=${currentTab}`)
            .then(response => {
                if (!response.ok) {
                    throw new Error('Network response was not ok');
                }
                return response.json();
            })
            .then(data => {
                if (data.success && data.data) {
                    renderNotifications(data.data);
                } else {
                    showEmptyState();
                }
            })
            .catch(error => {
                console.error('Error loading notifications:', error);
                listContainer.innerHTML = `
                    <div class="text-center py-4 text-danger">
                        <i class="bi bi-exclamation-triangle fs-3 d-block mb-2"></i>
                        <p class="mb-0">Không thể tải thông báo</p>
                    </div>
                `;
            });
    }

    // Render danh sách notifications
    function renderNotifications(notifications) {
        const listContainer = document.getElementById('notificationList');
        
        if (!notifications || notifications.length === 0) {
            showEmptyState();
            return;
        }

        listContainer.innerHTML = notifications.map(notif => {
            // Handle both camelCase and PascalCase property names
            const id = notif.notificationId || notif.NotificationId;
            const title = notif.title || notif.Title;
            const content = notif.content || notif.Content;
            const type = notif.type || notif.Type;
            const url = notif.url || notif.Url || '#';
            const isRead = notif.isRead || notif.IsRead || false;
            const createdAt = notif.createdAt || notif.CreatedAt;
            
            const icon = type.includes('Payment') || type.includes('Subscription') 
                ? '<i class="bi bi-credit-card"></i>' 
                : '<i class="bi bi-film"></i>';
            
            const iconClass = type.includes('Payment') || type.includes('Subscription')
                ? 'payment'
                : 'movie';

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
        }).join('');

        // Add click event cho từng notification item
        const notifItems = listContainer.querySelectorAll('.notification-item');
        notifItems.forEach(item => {
            item.addEventListener('click', function(e) {
                e.preventDefault();
                const notifId = this.dataset.id;
                const url = this.getAttribute('href');
                
                markAsRead(notifId, url);
            });
        });
    }

    function showEmptyState() {
        const listContainer = document.getElementById('notificationList');
        listContainer.innerHTML = `
            <div class="text-center py-5 text-muted">
                <i class="bi bi-bell-slash fs-1 d-block mb-3" style="opacity: 0.3;"></i>
                <p class="mb-0">Không có thông báo nào</p>
            </div>
        `;
    }

    // Update badges (số thông báo chưa đọc)
    function updateBadges() {
        fetch('/api/notifications/unread-count')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    // Update main badge
                    const mainBadge = document.querySelector('.notification-badge');
                    if (data.total > 0) {
                        mainBadge.textContent = data.total > 99 ? '99+' : data.total;
                        mainBadge.style.display = 'block';
                    } else {
                        mainBadge.style.display = 'none';
                    }

                    // Update tab badges
                    const paymentBadge = document.getElementById('paymentBadge');
                    const movieBadge = document.getElementById('movieBadge');

                    if (data.payment > 0) {
                        paymentBadge.textContent = data.payment;
                        paymentBadge.style.display = 'inline-block';
                    } else {
                        paymentBadge.style.display = 'none';
                    }

                    if (data.movie > 0) {
                        movieBadge.textContent = data.movie;
                        movieBadge.style.display = 'inline-block';
                    } else {
                        movieBadge.style.display = 'none';
                    }
                }
            })
            .catch(error => console.error('Error updating badges:', error));
    }

    // Đánh dấu 1 notification đã đọc
    function markAsRead(notifId, redirectUrl) {
        fetch(`/api/notifications/${notifId}/mark-read`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                updateBadges();
                
                // Redirect sau khi mark read
                if (redirectUrl && redirectUrl !== '#') {
                    window.location.href = redirectUrl;
                } else {
                    loadNotifications(); // Refresh list
                }
            }
        })
        .catch(error => console.error('Error marking as read:', error));
    }

    // Đánh dấu tất cả đã đọc
    function markAllAsRead() {
        if (!confirm('Đánh dấu tất cả thông báo là đã đọc?')) {
            return;
        }

        fetch(`/api/notifications/mark-all-read?type=${currentTab}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                updateBadges();
                loadNotifications();
                
                // Show success message
                showToast('success', data.message || 'Đã đánh dấu tất cả là đã đọc');
            }
        })
        .catch(error => {
            console.error('Error marking all as read:', error);
            showToast('error', 'Có lỗi xảy ra');
        });
    }

    // Helper functions
    function formatDateTime(dateStr) {
        const date = new Date(dateStr);
        const now = new Date();
        const diff = now - date;
        const seconds = Math.floor(diff / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);
        const days = Math.floor(hours / 24);

        if (seconds < 60) return 'Vừa xong';
        if (minutes < 60) return `${minutes} phút trước`;
        if (hours < 24) return `${hours} giờ trước`;
        if (days < 7) return `${days} ngày trước`;
        
        return date.toLocaleDateString('vi-VN');
    }

    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    function showToast(type, message) {
        // Simple toast notification
        const toast = document.createElement('div');
        toast.className = `alert alert-${type === 'success' ? 'success' : 'danger'} position-fixed`;
        toast.style.cssText = 'top: 20px; right: 20px; z-index: 99999; min-width: 250px;';
        toast.textContent = message;
        
        document.body.appendChild(toast);
        
        setTimeout(() => {
            toast.remove();
        }, 3000);
    }

})();