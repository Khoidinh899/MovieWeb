// ================================
// USER HISTORY PAGE HANDLER - FIXED
// ================================

document.addEventListener('DOMContentLoaded', () => {
    console.log('✅ User History page loaded');
    
    // Attach remove history handlers
    attachRemoveHistoryHandlers();
    
    // Attach clear all handler
    attachClearAllHandler();
});

// Attach event listeners to remove buttons
function attachRemoveHistoryHandlers() {
    const removeButtons = document.querySelectorAll('.remove-history');
    
    removeButtons.forEach(btn => {
        btn.addEventListener('click', async function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            const historyId = this.dataset.historyId;
            const movieCard = this.closest('.col-md-4, .col-sm-6');
            
            if (!historyId) {
                console.error('❌ History ID not found');
                return;
            }
            
            if (!confirm('Bạn có chắc muốn xóa phim này khỏi lịch sử xem?')) {
                return;
            }
            
            try {
                console.log('🗑️ Removing history:', historyId);
                
                const response = await fetch(`/api/watch-history/${historyId}`, {
                    method: 'DELETE',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    credentials: 'include'
                });
                
                if (response.ok) {
                    console.log('✅ Removed successfully');
                    showToast('Đã xóa khỏi lịch sử xem', 'success');
                    
                    // Remove card with animation
                    if (movieCard) {
                        movieCard.style.transition = 'all 0.3s ease';
                        movieCard.style.opacity = '0';
                        movieCard.style.transform = 'scale(0.8)';
                        
                        setTimeout(() => {
                            movieCard.remove();
                            
                            // Check if grid is empty
                            const remainingCards = document.querySelectorAll('#historyGrid .col-md-4, #historyGrid .col-sm-6');
                            if (remainingCards.length === 0) {
                                location.reload(); // Reload to show empty state
                            }
                        }, 300);
                    }
                } else {
                    const errorText = await response.text();
                    console.error('❌ Failed to remove:', response.status, errorText);
                    showToast('Không thể xóa lịch sử xem', 'danger');
                }
                
            } catch (error) {
                console.error('❌ Error removing history:', error);
                showToast('Đã xảy ra lỗi, vui lòng thử lại', 'danger');
            }
        });
    });
}

// Attach clear all handler
function attachClearAllHandler() {
    const clearAllBtn = document.getElementById('clearAllHistoryBtn');
    
    if (clearAllBtn) {
        clearAllBtn.addEventListener('click', async function(e) {
            e.preventDefault();
            
            if (!confirm('Bạn có chắc muốn xóa toàn bộ lịch sử xem?')) {
                return;
            }
            
            try {
                console.log('🗑️ Clearing all history');
                
                const response = await fetch('/api/watch-history/clear-all', {
                    method: 'DELETE',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    credentials: 'include'
                });
                
                if (response.ok) {
                    console.log('✅ Cleared all history');
                    showToast('Đã xóa toàn bộ lịch sử xem', 'success');
                    setTimeout(() => {
                        location.reload();
                    }, 1000);
                } else {
                    const errorText = await response.text();
                    console.error('❌ Failed to clear all:', response.status, errorText);
                    showToast('Không thể xóa toàn bộ lịch sử', 'danger');
                }
                
            } catch (error) {
                console.error('❌ Error clearing all history:', error);
                showToast('Đã xảy ra lỗi, vui lòng thử lại', 'danger');
            }
        });
    }
}

// Get anti-forgery token
function getAntiForgeryToken() {
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    return token ? token.value : '';
}

// Show toast notification
function showToast(message, type = 'success') {
    // Create toast container if not exists
    let container = document.getElementById('toastContainer');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastContainer';
        container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
    }
    
    // Create toast element
    const toastId = 'toast_' + Date.now();
    const toast = document.createElement('div');
    toast.id = toastId;
    toast.className = `toast align-items-center bg-${type} text-white border-0`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');
    
    const iconMap = {
        'success': 'check-circle',
        'danger': 'exclamation-circle',
        'warning': 'exclamation-triangle',
        'info': 'info-circle'
    };
    
    const icon = iconMap[type] || 'info-circle';
    
    toast.innerHTML = `
        <div class="d-flex">
            <div class="toast-body">
                <i class="fas fa-${icon} me-2"></i>${escapeHtml(message)}
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
        </div>
    `;
    
    container.appendChild(toast);
    
    // Initialize and show toast
    const bsToast = new bootstrap.Toast(toast, { delay: 3000 });
    bsToast.show();
    
    // Remove toast after hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
}

// Escape HTML to prevent XSS
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}