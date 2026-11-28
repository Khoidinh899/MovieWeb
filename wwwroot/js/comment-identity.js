// Danh tính khi bình luận (dùng chung V1 + V2):
// - Hiện tên hiển thị (họ tên hoặc biệt danh) thay cho email đăng nhập.
// - Gạt "Ẩn danh": bình luận/trả lời hiện bằng biệt danh, admin vẫn thấy người gốc.
// - Đổi biệt danh (giống biệt danh trong nhóm Facebook).
(function () {
    'use strict';

    var state = { displayName: '', nickname: '', loaded: false };

    function el(id) { return document.getElementById(id); }

    function initials(name) {
        var parts = (name || '').trim().split(/\s+/).filter(Boolean);
        if (!parts.length) return '?';
        var first = parts[0].charAt(0);
        var last = parts.length > 1 ? parts[parts.length - 1].charAt(0) : '';
        return (first + last).toUpperCase();
    }

    function isAnonymous() {
        var toggle = el('commentAnonymous');
        return !!(toggle && toggle.checked);
    }

    function render() {
        if (!state.loaded) return;
        var anonymous = isAnonymous();
        var name = anonymous ? state.nickname : state.displayName;
        var nameEl = el('commentDisplayName');
        var avatarEl = el('commentAvatarInitial');
        var hintEl = el('commentIdentityHint');
        if (nameEl) nameEl.textContent = name;
        if (avatarEl) avatarEl.textContent = anonymous ? '?' : initials(name);
        if (hintEl) {
            hintEl.textContent = anonymous
                ? 'Đang ẩn danh: mọi người chỉ thấy biệt danh "' + state.nickname + '".'
                : '';
            hintEl.style.display = anonymous ? '' : 'none';
        }
    }

    function notify(message, type) {
        if (typeof window.showNotification === 'function') window.showNotification(message, type);
        else alert(message);
    }

    async function load() {
        try {
            var res = await fetch('/api/comment/me', { credentials: 'include', headers: { 'Accept': 'application/json' } });
            if (!res.ok) return;
            var data = await res.json();
            if (!data.success) return;
            state.displayName = data.displayName || '';
            state.nickname = data.nickname || '';
            state.loaded = true;
            try { if (localStorage.getItem('mp_comment_anonymous') === '1' && el('commentAnonymous')) el('commentAnonymous').checked = true; } catch (e) { }
            render();
        } catch (e) {
            console.warn('comment identity load failed', e);
        }
    }

    function openNicknameForm() {
        var form = el('commentNicknameForm');
        var input = el('commentNicknameInput');
        if (!form || !input) return;
        input.value = state.nickname;
        form.style.display = 'flex';
        input.focus();
        input.select();
    }

    function closeNicknameForm() {
        var form = el('commentNicknameForm');
        if (form) form.style.display = 'none';
    }

    async function saveNickname() {
        var input = el('commentNicknameInput');
        if (!input) return;
        var value = input.value.trim();
        var body = new FormData();
        body.append('nickname', value);
        try {
            var res = await fetch('/api/comment/nickname', { method: 'POST', body: body, credentials: 'include', headers: { 'Accept': 'application/json' } });
            var data = await res.json();
            if (!data.success) { notify(data.message || 'Không đổi được biệt danh.', 'warning'); return; }
            state.nickname = data.nickname;
            await load(); // tên hiển thị có thể là biệt danh nếu chưa có họ tên
            closeNicknameForm();
            notify('Đã đổi biệt danh thành "' + data.nickname + '".', 'success');
            if (typeof window.reloadMovieComments === 'function') window.reloadMovieComments();
        } catch (e) {
            notify('Không đổi được biệt danh, vui lòng thử lại.', 'danger');
        }
    }

    function init() {
        var root = el('commentIdentity');
        if (!root || root.dataset.loggedIn !== 'true') return;
        el('commentAnonymous')?.addEventListener('change', function () {
            try { localStorage.setItem('mp_comment_anonymous', this.checked ? '1' : '0'); } catch (e) { }
            render();
        });
        el('commentNicknameEdit')?.addEventListener('click', function (e) { e.preventDefault(); openNicknameForm(); });
        el('commentNicknameSave')?.addEventListener('click', function (e) { e.preventDefault(); saveNickname(); });
        el('commentNicknameCancel')?.addEventListener('click', function (e) { e.preventDefault(); closeNicknameForm(); });
        el('commentNicknameInput')?.addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); saveNickname(); }
            if (e.key === 'Escape') { e.preventDefault(); closeNicknameForm(); }
        });
        load();
    }

    window.MoonCommentIdentity = { isAnonymous: isAnonymous };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
