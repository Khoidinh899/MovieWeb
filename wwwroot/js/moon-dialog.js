/* ==========================================================================
   MOONPHIM - LIQUID GLASS MODAL & BOTTOM ACTION SHEET SYSTEM (JS)
   ========================================================================== */

(function (window, document) {
    'use strict';

    var MoonDialog = {
        _overlay: null,
        _resolvePromise: null,

        _createDialogDOM: function () {
            if (this._overlay) return;

            var overlay = document.createElement('div');
            overlay.id = 'moonDialogOverlay';
            overlay.className = 'moon-dialog-overlay';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');

            overlay.innerHTML = 
                '<div class="moon-dialog-box">' +
                    '<div class="moon-dialog-icon danger" id="moonDialogIcon">' +
                        '<i class="bi bi-exclamation-triangle-fill"></i>' +
                    '</div>' +
                    '<h3 class="moon-dialog-title" id="moonDialogTitle">Xác nhận</h3>' +
                    '<p class="moon-dialog-desc" id="moonDialogDesc"></p>' +
                    '<div class="moon-dialog-actions" id="moonDialogActions">' +
                        '<button type="button" class="moon-btn moon-btn-cancel" id="moonDialogCancelBtn">Hủy</button>' +
                        '<button type="button" class="moon-btn moon-btn-danger" id="moonDialogConfirmBtn">Đồng ý</button>' +
                    '</div>' +
                '</div>';

            document.body.appendChild(overlay);
            this._overlay = overlay;

            // Click outside to cancel
            overlay.addEventListener('click', function (e) {
                if (e.target === overlay) {
                    MoonDialog._close(false);
                }
            });

            var cancelBtn = overlay.querySelector('#moonDialogCancelBtn');
            var confirmBtn = overlay.querySelector('#moonDialogConfirmBtn');

            cancelBtn.addEventListener('click', function () {
                MoonDialog._close(false);
            });

            confirmBtn.addEventListener('click', function () {
                MoonDialog._close(true);
            });

            // ESC key to close
            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape' && MoonDialog._overlay && MoonDialog._overlay.classList.contains('show')) {
                    MoonDialog._close(false);
                }
            });
        },

        _close: function (result) {
            if (!this._overlay) return;
            this._overlay.classList.remove('show');
            if (this._resolvePromise) {
                this._resolvePromise(result);
                this._resolvePromise = null;
            }
        },

        /**
         * Hiện hộp thoại xác nhận Kính Lỏng (Glass Confirm / Action Sheet)
         * @param {Object} options 
         * @param {string} options.title
         * @param {string} options.message
         * @param {string} [options.icon] - 'danger' | 'warning' | 'success' | 'info'
         * @param {string} [options.iconClass] - bootstrap icon class e.g. 'bi-trash3-fill'
         * @param {string} [options.confirmText] - e.g. 'Xóa ngay', 'Đồng ý'
         * @param {string} [options.cancelText] - e.g. 'Hủy bỏ'
         * @param {string} [options.type] - 'danger' | 'primary' | 'warning'
         * @returns {Promise<boolean>}
         */
        confirm: function (options) {
            this._createDialogDOM();

            if (typeof options === 'string') {
                options = { message: options };
            }

            var title = options.title || 'Xác nhận';
            var message = options.message || '';
            var type = options.type || 'danger';
            var confirmText = options.confirmText || 'Đồng ý';
            var cancelText = options.cancelText || 'Hủy';

            var iconType = options.icon || (type === 'danger' ? 'danger' : (type === 'warning' ? 'warning' : (type === 'success' ? 'success' : 'info')));
            var iconClass = options.iconClass || (type === 'danger' ? 'bi-trash3-fill' : (type === 'warning' ? 'bi-exclamation-triangle-fill' : (type === 'success' ? 'bi-check-circle-fill' : 'bi-info-circle-fill')));

            var iconEl = this._overlay.querySelector('#moonDialogIcon');
            var titleEl = this._overlay.querySelector('#moonDialogTitle');
            var descEl = this._overlay.querySelector('#moonDialogDesc');
            var cancelBtn = this._overlay.querySelector('#moonDialogCancelBtn');
            var confirmBtn = this._overlay.querySelector('#moonDialogConfirmBtn');

            iconEl.className = 'moon-dialog-icon ' + iconType;
            iconEl.innerHTML = '<i class="bi ' + iconClass + '"></i>';

            titleEl.textContent = title;
            descEl.innerHTML = message.replace(/\n/g, '<br>');

            cancelBtn.style.display = 'block';
            cancelBtn.textContent = cancelText;

            confirmBtn.className = 'moon-btn ' + (type === 'danger' ? 'moon-btn-danger' : 'moon-btn-primary');
            confirmBtn.textContent = confirmText;

            // Hiển thị Overlay
            this._overlay.classList.add('show');

            return new Promise(function (resolve) {
                MoonDialog._resolvePromise = resolve;
            });
        },

        /**
         * Hiện hộp thoại thông báo Kính Lỏng (Glass Alert)
         * @param {Object|string} options 
         * @returns {Promise<boolean>}
         */
        alert: function (options) {
            this._createDialogDOM();

            if (typeof options === 'string') {
                options = { message: options };
            }

            var title = options.title || 'Thông báo';
            var message = options.message || '';
            var type = options.type || 'info';
            var btnText = options.btnText || options.confirmText || 'Đã hiểu';

            var iconType = options.icon || (type === 'danger' ? 'danger' : (type === 'warning' ? 'warning' : (type === 'success' ? 'success' : 'info')));
            var iconClass = options.iconClass || (type === 'danger' ? 'bi-exclamation-octagon-fill' : (type === 'warning' ? 'bi-exclamation-triangle-fill' : (type === 'success' ? 'bi-check-circle-fill' : 'bi-bell-fill')));

            var iconEl = this._overlay.querySelector('#moonDialogIcon');
            var titleEl = this._overlay.querySelector('#moonDialogTitle');
            var descEl = this._overlay.querySelector('#moonDialogDesc');
            var cancelBtn = this._overlay.querySelector('#moonDialogCancelBtn');
            var confirmBtn = this._overlay.querySelector('#moonDialogConfirmBtn');

            iconEl.className = 'moon-dialog-icon ' + iconType;
            iconEl.innerHTML = '<i class="bi ' + iconClass + '"></i>';

            titleEl.textContent = title;
            descEl.innerHTML = message.replace(/\n/g, '<br>');

            cancelBtn.style.display = 'none'; // Ẩn nút Cancel trong Alert

            confirmBtn.className = 'moon-btn ' + (type === 'danger' ? 'moon-btn-danger' : 'moon-btn-primary');
            confirmBtn.textContent = btnText;

            // Hiển thị Overlay
            this._overlay.classList.add('show');

            return new Promise(function (resolve) {
                MoonDialog._resolvePromise = resolve;
            });
        }
    };

    window.MoonDialog = MoonDialog;
    window.moonConfirm = function (msg, title, type) {
        return MoonDialog.confirm({ message: msg, title: title || 'Xác nhận', type: type || 'danger' });
    };
    window.moonAlert = function (msg, title, type) {
        return MoonDialog.alert({ message: msg, title: title || 'Thông báo', type: type || 'info' });
    };

})(window, document);
