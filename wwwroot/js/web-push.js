// Thông báo đẩy trên trình duyệt/PWA (Web Push). Gắn vào mọi khối [data-webpush]:
//   [data-webpush-status] dòng trạng thái, [data-webpush-enable] / [data-webpush-disable] / [data-webpush-test] các nút.
// Chỉ xin quyền khi user bấm nút (không tự bật popup xin quyền).
(function () {
    'use strict';

    const boxes = document.querySelectorAll('[data-webpush]');
    if (!boxes.length) return;

    const supported = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;

    const token = () => (document.getElementById('RequestVerificationToken') || document.querySelector('input[name="__RequestVerificationToken"]'))?.value || '';

    const post = (url, body) => fetch(url, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
        body: body ? JSON.stringify(body) : null
    }).then(r => r.ok ? r.json() : Promise.reject(r.status));

    const urlBase64ToUint8Array = (base64) => {
        const padding = '='.repeat((4 - base64.length % 4) % 4);
        const raw = atob((base64 + padding).replace(/-/g, '+').replace(/_/g, '/'));
        return Uint8Array.from([...raw].map(c => c.charCodeAt(0)));
    };

    function render(state, message) {
        boxes.forEach(box => {
            const status = box.querySelector('[data-webpush-status]');
            if (status) status.textContent = message;
            box.dataset.webpushState = state;
            // style.display thay cho thuộc tính hidden: class .flex / .v2-btn-3d sẽ đè [hidden].
            box.querySelectorAll('[data-webpush-enable]').forEach(b => b.style.display = state === 'off' ? '' : 'none');
            box.querySelectorAll('[data-webpush-disable],[data-webpush-test]').forEach(b => b.style.display = state === 'on' ? '' : 'none');
            box.style.display = '';
        });
    }

    async function registration() {
        return navigator.serviceWorker.register('/sw.js', { scope: '/' });
    }

    async function refresh() {
        if (!supported) {
            render('unsupported', isIos && !isStandalone
                ? 'Trên iPhone/iPad: bấm nút Chia sẻ → "Thêm vào MH chính", mở MoonPhim từ biểu tượng đó rồi bật thông báo tại đây (cần iOS 16.4+).'
                : 'Trình duyệt này chưa hỗ trợ thông báo đẩy.');
            return;
        }
        if (Notification.permission === 'denied') {
            render('denied', 'Bạn đã chặn thông báo của MoonPhim. Hãy bật lại trong phần cài đặt trang web của trình duyệt.');
            return;
        }
        const reg = await registration();
        const sub = await reg.pushManager.getSubscription();
        if (sub && Notification.permission === 'granted') {
            // Đồng bộ lại với server (endpoint có thể đổi, hoặc vừa đổi tài khoản trên cùng trình duyệt).
            post('/api/web-push/subscribe', sub.toJSON()).catch(() => { });
            render('on', 'Đã bật thông báo trên thiết bị này.');
        } else {
            render('off', 'Nhận thông báo phim mới, phản hồi bình luận… ngay cả khi không mở MoonPhim.');
        }
    }

    async function enable(btn) {
        btn.disabled = true;
        try {
            const permission = await Notification.requestPermission();
            if (permission !== 'granted') { await refresh(); return; }
            const { publicKey } = await fetch('/api/web-push/public-key').then(r => r.json());
            const reg = await registration();
            await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: urlBase64ToUint8Array(publicKey) });
            await post('/api/web-push/subscribe', sub.toJSON());
            render('on', 'Đã bật thông báo trên thiết bị này.');
        } catch (e) {
            render('off', 'Chưa bật được thông báo, vui lòng thử lại.');
        } finally {
            btn.disabled = false;
        }
    }

    async function disable(btn) {
        btn.disabled = true;
        try {
            const reg = await registration();
            const sub = await reg.pushManager.getSubscription();
            if (sub) {
                await post('/api/web-push/unsubscribe', { endpoint: sub.endpoint }).catch(() => { });
                await sub.unsubscribe();
            }
            render('off', 'Đã tắt thông báo trên thiết bị này.');
        } finally {
            btn.disabled = false;
        }
    }

    async function test(btn) {
        btn.disabled = true;
        try {
            const result = await post('/api/web-push/test');
            render('on', result.success ? 'Đã gửi thông báo thử — kiểm tra góc màn hình nhé.' : 'Chưa gửi được thông báo thử.');
        } catch (e) {
            render('on', 'Chưa gửi được thông báo thử.');
        } finally {
            btn.disabled = false;
        }
    }

    document.addEventListener('click', (e) => {
        const target = e.target.closest('[data-webpush-enable],[data-webpush-disable],[data-webpush-test]');
        if (!target) return;
        if (target.hasAttribute('data-webpush-enable')) enable(target);
        else if (target.hasAttribute('data-webpush-disable')) disable(target);
        else test(target);
    });

    refresh().catch(() => render('off', 'Nhận thông báo phim mới, phản hồi bình luận… ngay cả khi không mở MoonPhim.'));
})();
