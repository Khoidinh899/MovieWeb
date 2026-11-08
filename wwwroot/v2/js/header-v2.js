// Header V2: lớp phủ tìm kiếm mobile. Không có JS thì icon kính lúp vẫn là link tới /tim-kiem.
(function () {
    'use strict';
    var openBtn = document.getElementById('v2SearchOpen');
    var overlay = document.getElementById('v2SearchOverlay');
    var closeBtn = document.getElementById('v2SearchClose');
    var input = document.getElementById('v2SearchOverlayInput');
    if (!openBtn || !overlay) return;

    function open(e) {
        if (e) e.preventDefault();
        overlay.classList.remove('hidden');
        overlay.classList.add('flex');
        document.body.style.overflow = 'hidden';
        document.body.classList.add('v2-search-open');
        if (input) setTimeout(function () { input.focus(); }, 30);
    }
    function close() {
        overlay.classList.add('hidden');
        overlay.classList.remove('flex');
        document.body.style.overflow = '';
        document.body.classList.remove('v2-search-open');
        openBtn.focus();
    }
    openBtn.addEventListener('click', open);
    if (closeBtn) closeBtn.addEventListener('click', close);
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape' && !overlay.classList.contains('hidden')) close();
    });
    // Rộng hơn mobile (xoay màn hình...) thì tự đóng
    window.matchMedia('(min-width: 768px)').addEventListener('change', function (m) {
        if (m.matches && !overlay.classList.contains('hidden')) close();
    });
})();


// Nút lên đầu trang (V1 có #scrollTopBtn): hiện khi cuộn > 200px, cuộn mượt về đầu.
(function () {
    var btn = document.getElementById('scrollTopBtn');
    if (!btn) return;
    var ticking = false;
    function update() {
        ticking = false;
        var y = window.pageYOffset || document.documentElement.scrollTop || 0;
        if (y > 200) { if (btn.hidden) btn.hidden = false; } else if (!btn.hidden) { btn.hidden = true; }
    }
    window.addEventListener('scroll', function () { if (!ticking) { ticking = true; requestAnimationFrame(update); } }, { passive: true });
    btn.addEventListener('click', function (e) { e.preventDefault(); window.scrollTo({ top: 0, behavior: 'smooth' }); });
    update();
})();


// Sidebar menu (mobile/tablet): nút 3 gạch mở panel trượt từ trái; đóng bằng nền mờ, nút X, Esc hoặc khi chọn liên kết.
(function () {
    var openBtn = document.getElementById('v2MenuOpen');
    var side = document.getElementById('v2Sidebar');
    if (!openBtn || !side) return;
    function setOpen(on) {
        side.classList.toggle('is-open', on);
        side.setAttribute('aria-hidden', on ? 'false' : 'true');
        openBtn.setAttribute('aria-expanded', on ? 'true' : 'false');
        document.body.style.overflow = on ? 'hidden' : '';
        document.body.classList.toggle('v2-sidebar-open', on);
        if (on) { var c = side.querySelector('[data-v2-sidebar-close].flex, button[data-v2-sidebar-close]'); if (c) c.focus(); } else { openBtn.focus(); }
    }
    openBtn.addEventListener('click', function () { setOpen(true); });
    side.querySelectorAll('[data-v2-sidebar-close]').forEach(function (el) { el.addEventListener('click', function () { setOpen(false); }); });
    side.querySelectorAll('a[href]').forEach(function (a) { a.addEventListener('click', function () { if (a.getAttribute('href') !== '#') setOpen(false); }); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && side.classList.contains('is-open')) setOpen(false); });
    side.querySelectorAll('[data-v2-sidebar-toggle]').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var box = document.getElementById(btn.getAttribute('data-v2-sidebar-toggle'));
            if (!box) return;
            var show = box.hidden;
            box.hidden = !show;
            btn.setAttribute('aria-expanded', show ? 'true' : 'false');
            btn.classList.toggle('is-open', show);
        });
    });
    window.matchMedia('(min-width: 1280px)').addEventListener('change', function (m) { if (m.matches && side.classList.contains('is-open')) setOpen(false); });
})();
