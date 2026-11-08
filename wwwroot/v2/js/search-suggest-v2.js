// Gợi ý tìm kiếm V2: gắn vào mọi input[data-v2-suggest]. Cùng API với V1 (wwwroot/js/search.js): /api/goi-y-tim-kiem.
(function () {
    'use strict';

    // Ảnh VSMov -> ảnh WebP w200 của trạm ảnh nội bộ (/img/w200/...webp); URL khác giữ nguyên. API JSON không đổi.
    const SRC = 'https://vsmov.com/storage/images/';
    const small = u => {
        if (typeof u !== 'string' || !u.startsWith(SRC)) return u;
        const rest = u.slice(SRC.length);
        return /^[A-Za-z0-9_\-./]+\.(jpg|jpeg|png|webp|gif)$/i.test(rest) ? '/img/w200/' + rest + '.webp' : u;
    };

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    function attach(input) {
        const host = input.closest('[data-v2-suggest-host]') || input.parentElement;
        host.classList.add('relative');

        const box = document.createElement('div');
        box.className = 'v2-suggest absolute left-0 right-0 top-[calc(100%+8px)] z-[1100] hidden max-h-[min(420px,70vh)] overflow-y-auto overscroll-contain rounded-2xl p-1.5';
        box.setAttribute('role', 'listbox');
        host.appendChild(box);
        input.setAttribute('aria-autocomplete', 'list');

        let timer, seq = 0, active = -1;

        const items = () => [...box.querySelectorAll('a[role="option"]')];
        const close = () => { box.classList.add('hidden'); active = -1; input.setAttribute('aria-expanded', 'false'); };
        const open = () => { box.classList.remove('hidden'); input.setAttribute('aria-expanded', 'true'); };
        const highlight = i => {
            const list = items();
            list.forEach((a, k) => a.classList.toggle('is-active', k === i));
            active = i;
            if (list[i]) list[i].scrollIntoView({ block: 'nearest' });
        };

        input.addEventListener('input', () => {
            clearTimeout(timer);
            const q = input.value.trim();
            if (q.length < 2) { close(); box.innerHTML = ''; return; }
            timer = setTimeout(async () => {
                const my = ++seq;
                try {
                    const res = await fetch(`/api/goi-y-tim-kiem?keyword=${encodeURIComponent(q)}`);
                    const data = await res.json();
                    if (my !== seq) return; // đã gõ tiếp, bỏ kết quả cũ
                    if (!Array.isArray(data) || data.length === 0) {
                        box.innerHTML = `<div class="px-4 py-3 text-sm text-on-surface-variant">Không có kết quả cho “${esc(q)}”</div>`;
                    } else {
                        box.innerHTML = data.map(m => `
                            <a role="option" href="/phim/${encodeURIComponent(m.slug || '')}" class="v2-suggest-item flex items-center gap-3 rounded-xl px-2.5 py-2 no-underline">
                                <img src="${esc(small(m.image) || '/images/default-poster.jpg')}" alt="" loading="lazy"
                                     class="aspect-video w-20 shrink-0 rounded-lg object-cover"
                                     onerror="this.onerror=null;this.src='/images/default-poster.jpg'">
                                <span class="min-w-0">
                                    <span class="block truncate text-sm font-semibold text-on-surface">${esc(m.name)}</span>
                                    ${m.year ? `<span class="text-xs text-on-surface-variant">${esc(m.year)}</span>` : ''}
                                </span>
                            </a>`).join('') +
                            `<a role="option" href="/tim-kiem?keyword=${encodeURIComponent(q)}" class="v2-suggest-item mt-1 flex items-center justify-center gap-1 rounded-xl px-3 py-2 text-sm font-semibold text-primary no-underline">
                                Xem tất cả kết quả cho “${esc(q)}”
                            </a>`;
                    }
                    active = -1;
                    box.scrollTop = 0;
                    open();
                } catch (e) {
                    close();
                }
            }, 250);
        });

        input.addEventListener('keydown', e => {
            if (box.classList.contains('hidden')) return;
            const list = items();
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(Math.min(active + 1, list.length - 1)); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(Math.max(active - 1, 0)); }
            else if (e.key === 'Enter' && list[active]) { e.preventDefault(); window.location.href = list[active].href; }
            else if (e.key === 'Escape') { close(); }
        });

        input.addEventListener('focus', () => { if (box.innerHTML && input.value.trim().length >= 2) open(); });
        document.addEventListener('click', e => { if (!host.contains(e.target)) close(); });
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('input[data-v2-suggest]').forEach(attach);
    });
})();
