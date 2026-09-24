// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// ==========================================================================
// MOONPHIM - PWA SHORTCUT & QUICK ACTION HANDLERS
// ==========================================================================
(function () {
    'use strict';

    function handlePwaShortcuts() {
        var urlParams = new URLSearchParams(window.location.search);
        if (urlParams.get('action') === 'remove-shortcut') {
            // Clean URL query parameter without reloading
            var cleanUrl = window.location.pathname;
            if (window.history && window.history.replaceState) {
                window.history.replaceState({}, document.title, cleanUrl);
            }

            var checkDialogInterval = setInterval(function () {
                if (window.MoonDialog) {
                    clearInterval(checkDialogInterval);
                    window.MoonDialog.confirm({
                        title: 'Xóa Phím Tắt Màn Hình Chính',
                        message: 'Để gỡ hoặc xóa phím tắt MoonPhim khỏi màn hình chính thiết bị:\n\n' +
                                 '• <b>Trên iPhone (iOS):</b> Nhấn giữ biểu tượng MoonPhim trên màn hình chính -> Chọn <b style="color:#ef4444;">"Xóa dấu trang"</b> (hoặc "Xóa ứng dụng").\n' +
                                 '• <b>Trên Android:</b> Nhấn giữ biểu tượng MoonPhim -> Chọn <b style="color:#ef4444;">"Gỡ cài đặt"</b> hoặc kéo vào thùng rác.\n\n' +
                                 'Bạn có muốn đặt lại cấu hình và hiển thị lại hướng dẫn cài đặt không?',
                        confirmText: 'Đặt lại cấu hình',
                        cancelText: 'Đóng',
                        type: 'danger',
                        iconClass: 'bi-trash3-fill'
                    }).then(function (confirmed) {
                        if (confirmed) {
                            localStorage.removeItem('moonphim_ios_prompt_dismissed');
                            localStorage.removeItem('pwa_installed');
                            if (window.MoonDialog.alert) {
                                window.MoonDialog.alert({
                                    title: 'Đã đặt lại',
                                    message: 'Đã đặt lại cấu hình phím tắt thành công! Bạn có thể thêm lại bất kỳ lúc nào.',
                                    type: 'success',
                                    btnText: 'Đã hiểu'
                                });
                            }
                        }
                    });
                }
            }, 100);

            // Timeout safety after 5s
            setTimeout(function () {
                clearInterval(checkDialogInterval);
            }, 5000);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', handlePwaShortcuts);
    } else {
        handlePwaShortcuts();
    }
})();
