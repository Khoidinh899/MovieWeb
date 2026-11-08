// Trang chủ V2: slider hero thuần JS (thay carousel Bootstrap của V1, vì V2 không nạp CSS Bootstrap).
// Không chặn tương tác: nút/indicator bấm được ngay, tự chạy 4s/slide và dừng khi người dùng bấm.
(function () {
    var root = document.getElementById('heroSlider');
    if (!root) return;

    var slides = root.querySelectorAll('.home-hero-slide');
    var dots = root.querySelectorAll('[data-slide-to]');
    var counter = document.getElementById('heroCounter');
    if (slides.length === 0) return;

    var current = 0;
    var timer = null;

    function show(index) {
        current = (index + slides.length) % slides.length;
        slides.forEach(function (s, i) {
            var on = i === current;
            s.classList.toggle('is-active', on);
            s.setAttribute('aria-hidden', on ? 'false' : 'true');
        });
        dots.forEach(function (d, i) { d.classList.toggle('is-active', i === current); });
        if (counter) counter.textContent = (current + 1) + ' / ' + slides.length;
    }

    function restart() {
        clearInterval(timer);
        timer = setInterval(function () { show(current + 1); }, 4000);
    }

    dots.forEach(function (d) {
        d.addEventListener('click', function () { show(parseInt(d.getAttribute('data-slide-to'), 10)); restart(); });
    });
    var prev = root.querySelector('[data-slide-prev]');
    var next = root.querySelector('[data-slide-next]');
    if (prev) prev.addEventListener('click', function () { show(current - 1); restart(); });
    if (next) next.addEventListener('click', function () { show(current + 1); restart(); });

    // Vuốt ngang trên mobile để chuyển banner (vuốt trái = slide kế, vuốt phải = slide trước). Không chặn cuộn dọc.
    if (slides.length > 1) {
        root.style.touchAction = 'pan-y';
        var sx = 0, sy = 0, tracking = false;
        root.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) { tracking = false; return; }
            tracking = true; sx = e.touches[0].clientX; sy = e.touches[0].clientY;
        }, { passive: true });
        root.addEventListener('touchend', function (e) {
            if (!tracking) return;
            tracking = false;
            var t = e.changedTouches[0], dx = t.clientX - sx, dy = t.clientY - sy;
            if (Math.abs(dx) < 40 || Math.abs(dx) < Math.abs(dy) * 1.5) return;
            show(dx < 0 ? current + 1 : current - 1);
            restart();
        }, { passive: true });
    }

    show(0);
    if (slides.length > 1) restart();
})();
