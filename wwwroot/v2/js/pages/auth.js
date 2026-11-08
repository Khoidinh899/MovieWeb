// Trang đăng nhập / đăng ký / quên mật khẩu / đặt lại mật khẩu V2.
// Gọi đúng endpoint, đúng field và cùng cách xử lý lỗi như wwwroot/js/auth.js (V1, dạng modal).
(function () {
    var alertBox = document.getElementById('authAlert');

    function showAlert(message, type) {
        if (!alertBox) return;
        var tone = type === 'success'
            ? 'bg-primary-container/20 text-primary'
            : type === 'warning'
                ? 'bg-secondary/15 text-secondary'
                : 'bg-error-container/40 text-error';
        alertBox.className = 'mb-4 rounded-lg px-4 py-3 text-sm ' + tone;
        alertBox.textContent = message;
        alertBox.classList.remove('hidden');
    }

    function setLoading(button, label, loading) {
        if (!button) return;
        button.disabled = loading;
        if (label) label.textContent = loading ? 'Đang xử lý...' : label.dataset.idle;
    }

    function antiForgeryToken(form) {
        var input = form.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function withLabel(id) {
        var el = document.getElementById(id);
        if (el) el.dataset.idle = el.textContent;
        return el;
    }

    // Turnstile: render widget nếu API đã nạp (script nằm trong layout); thử lại vài lần nếu chưa sẵn
    function renderTurnstile() {
        var container = document.getElementById('turnstile-container');
        if (!container || container.dataset.rendered === '1' || !window.turnstile) return false;
        var sitekey = container.dataset.sitekey;
        if (!sitekey) return false;
        window.turnstile.render('#turnstile-container', { sitekey: sitekey });
        container.dataset.rendered = '1';
        return true;
    }
    if (!renderTurnstile()) {
        var attempts = 0;
        var timer = setInterval(function () {
            attempts++;
            if (renderTurnstile() || attempts > 40) clearInterval(timer);
        }, 250);
    }

    // ===== Đăng nhập =====
    var loginForm = document.getElementById('loginFormSubmit');
    if (loginForm) {
        var loginBtn = document.getElementById('loginSubmit');
        var loginLabel = withLabel('loginBtnText');
        loginForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var returnUrl = loginForm.dataset.returnUrl || '';
            var url = '/Auth/Login' + (returnUrl ? '?returnUrl=' + encodeURIComponent(returnUrl) : '');
            setLoading(loginBtn, loginLabel, true);
            try {
                var response = await fetch(url, {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: new FormData(loginForm)
                });
                var result = await response.json();
                if (result.success) {
                    showAlert(result.message, 'success');
                    window.location.href = result.redirectUrl || '/trang-chu';
                    return;
                }
                showAlert(result.message, 'danger');
                if (result.message && result.message.indexOf('chưa được xác thực') !== -1) {
                    document.getElementById('resendBox')?.classList.remove('hidden');
                }
            } catch (err) {
                console.error('Login error:', err);
                showAlert('Có lỗi xảy ra khi đăng nhập', 'danger');
            } finally {
                setLoading(loginBtn, loginLabel, false);
            }
        });

        document.getElementById('resendVerification')?.addEventListener('click', async function () {
            var email = document.getElementById('loginEmail').value.trim();
            if (!email) {
                showAlert('Không tìm thấy email!', 'warning');
                return;
            }
            var data = new FormData();
            data.append('email', email);
            data.append('__RequestVerificationToken', antiForgeryToken(loginForm));
            try {
                var response = await fetch('/Auth/ResendEmailConfirmation', { method: 'POST', body: data });
                var result = await response.json();
                showAlert(result.message || (result.success ? 'Email xác thực đã được gửi lại!' : 'Gửi lại email xác thực thất bại!'),
                    result.success ? 'success' : 'danger');
            } catch (err) {
                showAlert('Có lỗi xảy ra. Vui lòng thử lại!', 'danger');
            }
        });
    }

    // ===== Đăng ký =====
    var registerForm = document.getElementById('registerFormSubmit');
    if (registerForm) {
        var regBtn = document.getElementById('registerSubmit');
        var regLabel = withLabel('registerBtnText');
        registerForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var password = document.getElementById('registerPassword').value;
            var confirm = document.getElementById('registerConfirmPassword').value;
            if (password !== confirm) {
                showAlert('Mật khẩu xác nhận không khớp!', 'danger');
                return;
            }
            if (password.length < 6) {
                showAlert('Mật khẩu phải có ít nhất 6 ký tự!', 'danger');
                return;
            }
            setLoading(regBtn, regLabel, true);
            try {
                var response = await fetch('/Auth/Register', {
                    method: 'POST',
                    headers: { 'X-Requested-With': 'XMLHttpRequest' },
                    body: new FormData(registerForm)
                });
                var result = null;
                try { result = await response.json(); } catch (parseErr) { result = null; }
                if (response.ok && result && result.success) {
                    showAlert(result.message || 'Đăng ký thành công! Vui lòng kiểm tra email để xác thực tài khoản.', 'success');
                    registerForm.reset();
                } else {
                    var message = result ? result.message : 'Đăng ký thất bại';
                    if (result && result.errors && result.errors.length > 0) message = result.errors.join(', ');
                    showAlert(message || 'Đăng ký thất bại, vui lòng kiểm tra lại!', 'danger');
                }
            } catch (err) {
                showAlert('Có lỗi xảy ra khi đăng ký: ' + (err.message || 'Vui lòng thử lại sau!'), 'danger');
            } finally {
                setLoading(regBtn, regLabel, false);
            }
        });
    }

    // ===== Quên mật khẩu =====
    var forgotForm = document.getElementById('forgotPasswordFormSubmit');
    if (forgotForm) {
        var forgotBtn = document.getElementById('forgotSubmit');
        var forgotLabel = withLabel('forgotBtnText');
        forgotForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var email = document.getElementById('forgotEmail').value.trim();
            if (!email) {
                showAlert('Vui lòng nhập email hợp lệ!', 'warning');
                return;
            }
            var data = new FormData();
            data.append('Email', email);
            data.append('__RequestVerificationToken', antiForgeryToken(forgotForm));
            setLoading(forgotBtn, forgotLabel, true);
            try {
                var response = await fetch('/Auth/ForgotPassword', { method: 'POST', body: data });
                if (response.ok) {
                    showAlert('Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu. Vui lòng kiểm tra hộp thư!', 'success');
                    forgotForm.reset();
                } else {
                    showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
                }
            } catch (err) {
                showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
            } finally {
                setLoading(forgotBtn, forgotLabel, false);
            }
        });
    }

    // ===== Đặt lại mật khẩu =====
    var resetForm = document.getElementById('resetPasswordFormSubmit');
    if (resetForm) {
        var resetBtn = document.getElementById('resetSubmit');
        var resetLabel = withLabel('resetBtnText');
        resetForm.addEventListener('submit', async function (e) {
            e.preventDefault();
            var newPassword = document.getElementById('newPassword').value;
            var confirm = document.getElementById('confirmNewPassword').value;
            if (newPassword !== confirm) {
                showAlert('Mật khẩu xác nhận không khớp!', 'danger');
                return;
            }
            if (newPassword.length < 6) {
                showAlert('Mật khẩu phải có ít nhất 6 ký tự!', 'danger');
                return;
            }
            setLoading(resetBtn, resetLabel, true);
            try {
                var response = await fetch('/Auth/ResetPassword', { method: 'POST', body: new FormData(resetForm) });
                if (response.ok) {
                    showAlert('Đặt lại mật khẩu thành công! Đang chuyển đến trang đăng nhập...', 'success');
                    setTimeout(function () { window.location.href = '/Auth/Login'; }, 1200);
                } else {
                    showAlert('Đặt lại mật khẩu thất bại. Link có thể đã hết hạn hoặc không hợp lệ!', 'danger');
                }
            } catch (err) {
                showAlert('Có lỗi xảy ra khi đặt lại mật khẩu.', 'danger');
            } finally {
                setLoading(resetBtn, resetLabel, false);
            }
        });
    }
})();
