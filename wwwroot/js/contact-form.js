// Form "Gửi tin nhắn nhanh" ở /contact (V1 + V2): gửi thật tới /contact/send → email contact@moonphim.me.
(function () {
    'use strict';
    const form = document.getElementById('quickContactForm');
    if (!form) return;

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        const button = form.querySelector('button[type="submit"]');
        const label = button ? button.textContent : '';
        if (button) { button.disabled = true; button.textContent = 'Đang gửi...'; }
        try {
            const res = await fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin' });
            const data = await res.json().catch(() => ({}));
            if (res.ok && data.success) {
                alert('Cảm ơn bạn! Tin nhắn đã được gửi tới MoonPhim. Chúng tôi sẽ phản hồi qua email sớm nhất có thể.');
                form.reset();
            } else {
                alert(data.message || 'Chưa gửi được tin nhắn, vui lòng thử lại sau.');
            }
        } catch (err) {
            alert('Chưa gửi được tin nhắn, vui lòng kiểm tra kết nối và thử lại.');
        } finally {
            if (button) { button.disabled = false; button.textContent = label; }
        }
    });
})();
