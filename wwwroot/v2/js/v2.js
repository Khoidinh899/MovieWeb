// V2 UI shared behaviour (vanilla JS, no Bootstrap). Page-specific code goes in wwwroot/v2/js/pages/*.js.
(function () {
    'use strict';
    window.MoonPhimV2 = window.MoonPhimV2 || {};

    // Header: same behaviour as V1 (_Scripts.cshtml). .is-scrolled after 50px; CSS in ClientApp-v2/src/v2.css.
    var header = document.querySelector('.v2-header');
    if (!header) return;
    var ticking = false;

    function measure() {
        document.documentElement.style.setProperty('--v2-header-h', header.offsetHeight + 'px');
    }
    function update() {
        var y = window.scrollY || document.documentElement.scrollTop || 0;
        header.classList.toggle('is-scrolled', y > 50);
        ticking = false;
    }
    function onScroll() {
        if (!ticking) { ticking = true; window.requestAnimationFrame(update); }
    }

    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', function () { measure(); onScroll(); });
    measure();
    update();
})();
