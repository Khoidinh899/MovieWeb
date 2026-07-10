// ================================
// FAVORITE BUTTON HANDLER
// ================================

class FavoriteHandler {
    constructor(movieId) {
        this.movieId = movieId;
        this.button = null;
        this.isFavorited = false;
        this.isLoading = false;
    }

    // Initialize
    async init() {
        this.button = document.getElementById('favoriteBtn');
        if (!this.button) {
            console.error('❌ Favorite button not found');
            return;
        }

        // console.log('✅ Favorite Handler initialized for movie:', this.movieId);

        // ✅ SỬA: Lấy từ window.isLoggedIn (đã khai báo trong Detail.cshtml)
        const isLoggedIn = window.isLoggedIn;
        // console.log('🔐 User logged in (from window):', isLoggedIn);

        // Check if movie is favorited (chỉ khi đã đăng nhập)
        if (isLoggedIn) {
            await this.checkFavoriteStatus();
        }

        // Attach click event
        this.button.addEventListener('click', (e) => {
            e.preventDefault(); // ✅ THÊM: Ngăn form submit
            e.stopPropagation(); // ✅ THÊM: Ngăn event bubbling
            this.toggleFavorite();
        });
    }

    // Check favorite status
    async checkFavoriteStatus() {
        try {
            // console.log('🔍 Checking favorite status for movie:', this.movieId);

            const response = await fetch(`/api/favorites/check/${this.movieId}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                },
                credentials: 'include'
            });

            if (response.ok) {
                const data = await response.json();
                this.isFavorited = data.isFavorited;
                // console.log('✅ Favorite status:', this.isFavorited);
                this.updateButtonUI();
            } else {
                // console.warn('⚠️ Failed to check favorite status:', response.status);
            }
        } catch (error) {
            console.error('❌ Error checking favorite status:', error);
        }
    }

    // Toggle favorite
    async toggleFavorite() {
        if (this.isLoading) {
            // console.log('⚠️ Already processing...');
            return;
        }

        // ✅ SỬA: Lấy từ window.isLoggedIn
        const isLoggedIn = window.isLoggedIn;

        // console.log('🖱️ Toggle favorite clicked - Logged in:', isLoggedIn);

        if (!isLoggedIn) {
            this.showToast('Vui lòng đăng nhập để thêm vào yêu thích', 'warning');
            setTimeout(() => {
                const returnUrl = encodeURIComponent(window.location.pathname);
                window.location.href = `/auth/login?returnUrl=${returnUrl}`;
            }, 1500);
            return;
        }

        this.isLoading = true;
        this.button.classList.add('loading');
        this.button.disabled = true; // ✅ THÊM: Disable button khi đang xử lý

        try {
            if (this.isFavorited) {
                await this.removeFavorite();
            } else {
                await this.addFavorite();
            }
        } catch (error) {
            console.error('❌ Error toggling favorite:', error);
            this.showToast('Đã xảy ra lỗi, vui lòng thử lại', 'danger');
        } finally {
            this.isLoading = false;
            this.button.classList.remove('loading');
            this.button.disabled = false; // ✅ THÊM: Enable lại button
        }
    }

    // Add to favorites
    async addFavorite() {
        // console.log('➕ Adding movie to favorites:', this.movieId);

        const response = await fetch(`/api/favorites/${this.movieId}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include'
        });

        if (response.ok) {
            this.isFavorited = true;
            this.updateButtonUI();
            this.showToast('Đã thêm vào danh sách yêu thích', 'success');
            // console.log('✅ Added to favorites successfully');
        } else {
            const error = await response.text();
            console.error('❌ Failed to add favorite:', response.status, error);
            throw new Error('Failed to add favorite');
        }
    }

    // Remove from favorites
    async removeFavorite() {
        // console.log('➖ Removing movie from favorites:', this.movieId);

        const response = await fetch(`/api/favorites/${this.movieId}`, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include'
        });

        if (response.ok) {
            this.isFavorited = false;
            this.updateButtonUI();
            this.showToast('Đã xóa khỏi danh sách yêu thích', 'success');
            // console.log('✅ Removed from favorites successfully');
        } else {
            const error = await response.text();
            console.error('❌ Failed to remove favorite:', response.status, error);
            throw new Error('Failed to remove favorite');
        }
    }

    // Update button UI
    updateButtonUI() {
        const icon = this.button.querySelector('i');
        const text = this.button.querySelector('span');

        if (this.isFavorited) {
            this.button.classList.add('active');
            icon.className = 'fas fa-heart';
            if (text) text.textContent = 'Đã yêu thích';
        } else {
            this.button.classList.remove('active');
            icon.className = 'far fa-heart';
            if (text) text.textContent = 'Yêu thích';
        }
    }

    // Show toast notification
    showToast(message, type = 'success') {
        const toastHtml = `
            <div class="toast align-items-center bg-${type} text-white border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas fa-${type === 'success' ? 'check-circle' : type === 'warning' ? 'exclamation-triangle' : 'exclamation-circle'}"></i>
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(container);
        }

        container.insertAdjacentHTML('beforeend', toastHtml);

        const toastElement = container.lastElementChild;
        const toast = new bootstrap.Toast(toastElement, { delay: 3000 });
        toast.show();

        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    }
}

// Auto-initialize if movieId exists
document.addEventListener('DOMContentLoaded', () => {
    // console.log('🚀 DOM loaded - Initializing favorite handler...');

    // ✅ SỬA: Lấy từ window.movieId (đã khai báo trong Detail.cshtml)
    const movieId = window.movieId;

    if (movieId) {
        // console.log('🎬 Initializing favorite handler for movie ID:', movieId);
        const favoriteHandler = new FavoriteHandler(movieId);
        favoriteHandler.init();
    } else {
        // console.warn('⚠️ Movie ID not found - Favorite handler not initialized');
    }
});