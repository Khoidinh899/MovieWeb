// V2 mobile tab bar: V1 "liquid glass" behaviour (orb position, active tab, "Đang xem") without the V1 SPA navigation.
// Logic ported from wwwroot/js/mobile-bottom-nav.js; links use normal page loads.
(function () {
    'use strict';
    var nav = document.getElementById('mobileBottomNav');
    if (!nav) return;

    function targetForPath(path) {
        path = (path || '').toLowerCase();
        if (path === '/' || path === '/trang-chu' || path === '/trangchu' || path === '') return 'home';
        if (path.indexOf('/phim/') === 0) return 'watching';
        if (/phim-moi|phim-le|phim-bo|hoat-hinh|\/the-loai|\/quoc-gia/.test(path)) return 'new';
        if (/\/user\/history|\/user\/favorite|lich-su/.test(path)) return 'history';
        if (/\/user\/(profile|edit|change-password|payment|notifications)|tai-khoan/.test(path)) return 'profile';
        return null;
    }

    function placeOrb(item) {
        var dock = nav.querySelector('.liquid-glass-dock');
        var orb = document.getElementById('liquidSlidingOrb');
        var orbIcon = document.getElementById('liquidOrbIcon');
        if (!dock || !orb) return;
        if (!item) { orb.classList.remove('active'); return; }
        var dockRect = dock.getBoundingClientRect();
        var itemRect = item.getBoundingClientRect();
        if (itemRect.width === 0) return;
        var x = itemRect.left - dockRect.left + itemRect.width / 2 - 24; // orb is 48px wide
        orb.style.transform = 'translate3d(' + x + 'px, 0, 0)';
        orb.classList.add('active');
        var icon = item.getAttribute('data-icon');
        if (icon && orbIcon) orbIcon.className = 'liquid-orb-icon bi ' + icon;
    }

    function setActive(target) {
        var active = null;
        nav.querySelectorAll('.liquid-nav-item').forEach(function (el) {
            var on = !!target && el.getAttribute('data-nav-target') === target;
            el.classList.toggle('active', on);
            if (on) active = el;
        });
        placeOrb(active);
    }

    function goNowPlaying() {
        if (location.pathname.indexOf('/phim/') === 0) {
            (document.getElementById('videoContainer') || document.body).scrollIntoView({ behavior: 'smooth', block: 'start' });
            return;
        }
        var last = null;
        try { last = JSON.parse(localStorage.getItem('moon_current_watching') || 'null'); } catch (e) { }
        if (last && last.url && last.url.indexOf('/phim/') === 0) { location.href = last.url; return; }

        fetch('/api/watch-history?page=1&pageSize=1', { headers: { 'Accept': 'application/json' } })
            .then(function (r) { if (!r.ok) throw new Error('history'); return r.json(); })
            .then(function (data) {
                var item = (data && Array.isArray(data.movies) && data.movies[0]) ||
                    (data && data.items && data.items[0]) || (Array.isArray(data) && data[0]) || null;
                if (!item || !item.slug) throw new Error('empty');
                var url = '/phim/' + item.slug;
                try {
                    localStorage.setItem('moon_current_watching', JSON.stringify({ url: url, slug: item.slug, name: item.name || '', updatedAt: Date.now() }));
                } catch (e) { }
                location.href = url;
            })
            .catch(function () {
                var explore = window.confirm('Bạn chưa xem bộ phim nào nè! Khám phá phim mới tại MoonPhim nhé 🍿');
                location.href = explore ? '/the-loai/phim-moi-cap-nhat' : '/trang-chu';
            });
    }

    nav.querySelectorAll('.liquid-nav-item').forEach(function (item) {
        item.addEventListener('click', function (e) {
            var target = item.getAttribute('data-nav-target');
            var href = item.getAttribute('href');
            if (target === 'watching') { e.preventDefault(); goNowPlaying(); return; }
            var loggedIn = document.body.getAttribute('data-user-logged-in') === 'true';
            if ((target === 'profile' || target === 'history') && !loggedIn) {
                e.preventDefault();
                location.href = '/Auth/Login?returnUrl=' + encodeURIComponent(href);
                return;
            }
            if (target === targetForPath(location.pathname) && location.pathname === href) {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            }
        });
    });

    var current = targetForPath(location.pathname);
    setTimeout(function () { setActive(current); }, 80);
    window.addEventListener('resize', function () { setActive(current); });
})();
