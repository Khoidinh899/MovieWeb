/* MoonPhim service worker — CHỈ nhận thông báo đẩy (Web Push).
   Cố ý KHÔNG có sự kiện 'fetch': không cache, không chặn request nào (player, quảng cáo, API giữ nguyên). */
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

self.addEventListener('push', (event) => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data ? event.data.text() : '' }; }

    const title = data.title || 'MoonPhim';
    const options = {
        body: data.body || '',
        icon: data.icon || '/apple-touch-icon.png',
        badge: data.badge || '/favicon-32x32.png',
        tag: data.tag || undefined,
        renotify: !!data.tag,
        data: { url: data.url || '/trang-chu' }
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    // Chỉ mở trang trong chính MoonPhim.
    let target;
    try { target = new URL(event.notification.data && event.notification.data.url || '/trang-chu', self.location.origin); }
    catch (e) { target = new URL('/trang-chu', self.location.origin); }
    if (target.origin !== self.location.origin) target = new URL('/trang-chu', self.location.origin);

    event.waitUntil((async () => {
        const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
        for (const client of windows) {
            if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
                await client.focus();
                if ('navigate' in client) return client.navigate(target.href);
                return;
            }
        }
        return self.clients.openWindow(target.href);
    })());
});
