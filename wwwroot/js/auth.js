document.addEventListener('DOMContentLoaded', function () {
    initializeAuth();
});

function initializeAuth() {
    const authModal = new bootstrap.Modal(document.getElementById('authModal'));
    const loginBtn = document.getElementById('loginBtn');
    const mobileLoginBtn = document.getElementById('mobileLoginBtn');
    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');
    const forgotPasswordForm = document.getElementById('forgotPasswordForm');
    const resetPasswordForm = document.getElementById('resetPasswordForm');
    const verificationSuccess = document.getElementById('verificationSuccess');

    setupFormToggles();
    setupModalTriggers();
    setupFormSubmissions();
    setupLogout();
    checkResetPasswordURL();

    // ===== Form Toggle Functions =====
    function setupFormToggles() {
        document.getElementById('showRegister')?.addEventListener('click', e => {
            e.preventDefault();
            switchToRegister();
        });

        document.getElementById('showLogin')?.addEventListener('click', e => {
            e.preventDefault();
            switchToLogin();
        });

        document.getElementById('backToLogin')?.addEventListener('click', e => {
            e.preventDefault();
            switchToLogin();
        });

        // NEW: Forgot Password toggles
        document.getElementById('showForgotPassword')?.addEventListener('click', e => {
            e.preventDefault();
            switchToForgotPassword();
        });

        document.getElementById('backToLoginFromForgot')?.addEventListener('click', e => {
            e.preventDefault();
            switchToLogin();
        });
    }

    function switchToRegister() {
        fadeOut(loginForm, () => {
            hideAllForms();
            registerForm.classList.remove('d-none');
            document.querySelector('.modal-title').textContent = 'Đăng ký';
            fadeIn(registerForm);
        });
    }

    function switchToLogin() {
        const activeForms = [registerForm, verificationSuccess, forgotPasswordForm, resetPasswordForm];
        const activeForm = activeForms.find(form => !form.classList.contains('d-none')) || registerForm;

        fadeOut(activeForm, () => {
            hideAllForms();
            loginForm.classList.remove('d-none');
            document.querySelector('.modal-title').textContent = 'Đăng nhập';
            document.getElementById('emailVerificationAlert')?.classList.add('d-none');
            fadeIn(loginForm);
        });
    }

    function switchToForgotPassword() {
        fadeOut(loginForm, () => {
            hideAllForms();
            forgotPasswordForm.classList.remove('d-none');
            document.querySelector('.modal-title').textContent = 'Quên mật khẩu';
            fadeIn(forgotPasswordForm);
        });
    }

    function switchToResetPassword(userId, token) {
        hideAllForms();
        resetPasswordForm.classList.remove('d-none');
        document.querySelector('.modal-title').textContent = 'Đặt lại mật khẩu';
        document.getElementById('resetUserId').value = userId;
        document.getElementById('resetToken').value = token;
        fadeIn(resetPasswordForm);
    }

    function hideAllForms() {
        loginForm.classList.add('d-none');
        registerForm.classList.add('d-none');
        forgotPasswordForm.classList.add('d-none');
        resetPasswordForm.classList.add('d-none');
        verificationSuccess.classList.add('d-none');
    }

    // ===== Check URL for Reset Password =====
    function checkResetPasswordURL() {
        const urlParams = new URLSearchParams(window.location.search);
        const openResetPassword = urlParams.get('openResetPassword');
        const userId = urlParams.get('userId');
        const token = urlParams.get('token');

        if (openResetPassword === 'true' && userId && token) {
            switchToResetPassword(userId, token);
            authModal.show();

            // Clean URL sau khi mở modal
            window.history.replaceState({}, document.title, window.location.pathname);
        }
    }

    // ===== Modal Trigger Functions =====
    function setupModalTriggers() {
        if (loginBtn) {
            loginBtn.addEventListener('click', e => {
                e.preventDefault();
                authModal.show();
            });
        }
        if (mobileLoginBtn) {
            mobileLoginBtn.addEventListener('click', e => {
                e.preventDefault();
                authModal.show();
            });
        }
        document.getElementById('authModal').addEventListener('hidden.bs.modal', resetModalState);
    }

    function resetModalState() {
        document.getElementById('loginFormSubmit')?.reset();
        document.getElementById('registerFormSubmit')?.reset();
        document.getElementById('forgotPasswordFormSubmit')?.reset();
        document.getElementById('resetPasswordFormSubmit')?.reset();
        hideAllForms();
        loginForm.classList.remove('d-none');
        document.querySelector('.modal-title').textContent = 'Đăng nhập';
        document.getElementById('emailVerificationAlert')?.classList.add('d-none');
        resetLoadingState('login');
        resetLoadingState('register');
        resetLoadingState('forgot');
        resetLoadingState('reset');
    }

    // ===== Form Submission Functions =====
    function setupFormSubmissions() {
        document.getElementById('loginFormSubmit')?.addEventListener('submit', async e => {
            e.preventDefault();
            await handleLogin();
        });

        document.getElementById('registerFormSubmit')?.addEventListener('submit', async e => {
            e.preventDefault();
            await handleRegister();
        });

        // NEW: Forgot Password submission
        document.getElementById('forgotPasswordFormSubmit')?.addEventListener('submit', async e => {
            e.preventDefault();
            await handleForgotPassword();
        });

        // NEW: Reset Password submission
        document.getElementById('resetPasswordFormSubmit')?.addEventListener('submit', async e => {
            e.preventDefault();
            await handleResetPassword();
        });

        document.getElementById('resendVerification')?.addEventListener('click', async e => {
            e.preventDefault();
            await handleResendVerification();
        });
    }

    async function handleLogin() {
        const formData = new FormData(document.getElementById('loginFormSubmit'));
        setLoadingState('login', true);

        try {
            const response = await fetch('/Auth/Login', {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                showAlert(result.message, 'success');
                authModal.hide();

                setTimeout(() => {
                    window.location.href = result.redirectUrl;
                }, 500);
            } else {
                showAlert(result.message, 'danger');

                // Nếu email chưa xác thực, hiện alert
                if (result.message && result.message.includes('chưa được xác thực')) {
                    const emailVerificationAlert = document.getElementById('emailVerificationAlert');
                    if (emailVerificationAlert) {
                        emailVerificationAlert.classList.remove('d-none');
                        const email = document.getElementById('loginEmail').value;
                        emailVerificationAlert.dataset.email = email;
                    }
                }
            }
        } catch (error) {
            console.error('Login error:', error);
            showAlert('Có lỗi xảy ra khi đăng nhập', 'danger');
        } finally {
            setLoadingState('login', false);
        }
    }

    async function handleRegister() {
        const formData = new FormData(document.getElementById('registerFormSubmit'));
        const password = document.getElementById('registerPassword').value;
        const confirmPassword = document.getElementById('registerConfirmPassword').value;

        // Validate password match
        if (password !== confirmPassword) {
            showAlert('Mật khẩu xác nhận không khớp!', 'danger');
            return;
        }

        setLoadingState('register', true);

        try {
            const response = await fetch('/Auth/Register', {
                method: 'POST',
                body: formData
            });

            if (response.redirected || response.ok) {
                fadeOut(registerForm, () => {
                    hideAllForms();
                    verificationSuccess.classList.remove('d-none');
                    document.querySelector('.modal-title').textContent = 'Đăng ký thành công';
                    fadeIn(verificationSuccess);
                });
                showAlert('Đăng ký thành công! Vui lòng kiểm tra email để xác thực tài khoản.', 'success');
            } else {
                const html = await response.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const errorSummary = doc.querySelector('#registerErrorSummary');
                let errorMessage = errorSummary ? errorSummary.textContent.trim() : null;

                if (!errorMessage) {
                    const validationErrors = doc.querySelectorAll('.text-danger');
                    const errors = Array.from(validationErrors)
                        .map(el => el.textContent.trim())
                        .filter(text => text);
                    errorMessage = errors.length > 0 ? errors.join(', ') : 'Đăng ký thất bại, vui lòng kiểm tra lại!';
                }

                showAlert(errorMessage, 'danger');
            }
        } catch (error) {
            console.error('Register error:', error);
            showAlert('Có lỗi xảy ra khi đăng ký: ' + error.message, 'danger');
        } finally {
            setLoadingState('register', false);
        }
    }

    // NEW: Handle Forgot Password
    async function handleForgotPassword() {
        const email = document.getElementById('forgotEmail').value.trim();

        if (!email || !isValidEmail(email)) {
            showAlert('Vui lòng nhập email hợp lệ!', 'warning');
            return;
        }

        setLoadingState('forgot', true);

        const formData = new FormData();
        formData.append('Email', email);
        formData.append('__RequestVerificationToken', getAntiForgeryToken());

        try {
            const response = await fetch('/Auth/ForgotPassword', {
                method: 'POST',
                body: formData
            });

            if (response.ok) {
                showAlert('Nếu email tồn tại, chúng tôi đã gửi link đặt lại mật khẩu. Vui lòng kiểm tra hộp thư!', 'success');

                setTimeout(() => {
                    switchToLogin();
                    document.getElementById('forgotEmail').value = '';
                }, 2000);
            } else {
                showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
            }
        } catch (error) {
            console.error('Forgot password error:', error);
            showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
        } finally {
            setLoadingState('forgot', false);
        }
    }

    // NEW: Handle Reset Password
async function handleResetPassword() {
    const newPassword = document.getElementById('resetNewPassword').value;
    const confirmPassword = document.getElementById('resetConfirmPassword').value;

    // Validate
    if (newPassword !== confirmPassword) {
        showAlert('Mật khẩu xác nhận không khớp!', 'danger');
        return;
    }

    if (newPassword.length < 6) {
        showAlert('Mật khẩu phải có ít nhất 6 ký tự!', 'danger');
        return;
    }

    setLoadingState('reset', true);

    const formData = new FormData(document.getElementById('resetPasswordFormSubmit'));

    try {
        const response = await fetch('/Auth/ResetPassword', {
            method: 'POST',
            body: formData  // FormData tự động gửi Anti-Forgery Token
        });

        if (response.ok) {
            showAlert('Đặt lại mật khẩu thành công! Đang chuyển đến trang đăng nhập...', 'success');
            
            setTimeout(() => {
                const authModal = bootstrap.Modal.getInstance(document.getElementById('authModal'));
                if (authModal) {
                    authModal.hide();
                }
                window.location.href = '/';
            }, 2000);
        } else {
            const text = await response.text();
            console.error('Reset password failed:', text);
            showAlert('Đặt lại mật khẩu thất bại. Link có thể đã hết hạn hoặc không hợp lệ!', 'danger');
        }
    } catch (error) {
        console.error('Reset password error:', error);
        showAlert('Có lỗi xảy ra khi đặt lại mật khẩu.', 'danger');
    } finally {
        setLoadingState('reset', false);
    }
}
    // NEW: Handle Resend Email Verification
    async function handleResendVerification() {
        const emailVerificationAlert = document.getElementById('emailVerificationAlert');
        const email = emailVerificationAlert?.dataset.email;

        if (!email) {
            showAlert('Không tìm thấy email!', 'warning');
            return;
        }

        try {
            const formData = new FormData();
            formData.append('email', email);
            formData.append('__RequestVerificationToken', getAntiForgeryToken());

            const response = await fetch('/Auth/ResendEmailConfirmation', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                showAlert(result.message || 'Email xác thực đã được gửi lại!', 'success');
                emailVerificationAlert.classList.add('d-none');
            } else {
                showAlert(result.message || 'Không thể gửi lại email!', 'danger');
            }
        } catch (error) {
            console.error('Resend verification error:', error);
            showAlert('Có lỗi xảy ra. Vui lòng thử lại!', 'danger');
        }
    }

    // ===== Logout Functions =====
    function setupLogout() {
        const logoutBtn = document.getElementById('logoutBtn');
        const mobileLogoutBtn = document.getElementById('mobileLogoutBtn');

        if (logoutBtn) logoutBtn.addEventListener('click', e => { e.preventDefault(); handleLogout(); });
        if (mobileLogoutBtn) mobileLogoutBtn.addEventListener('click', e => { e.preventDefault(); handleLogout(); });
    }

    async function handleLogout() {
        try {
            const formData = new FormData();
            formData.append('__RequestVerificationToken', getAntiForgeryToken());

            const response = await fetch('/Auth/Logout', {
                method: 'POST',
                body: formData
            });

            if (response.ok) {
                showAlert('Đăng xuất thành công!', 'success');
                setTimeout(() => {
                    window.location.href = '/';
                }, 1000);
            } else {
                showAlert('Có lỗi xảy ra khi đăng xuất.', 'danger');
            }
        } catch (error) {
            console.error('Logout error:', error);
            showAlert('Có lỗi xảy ra khi đăng xuất.', 'danger');
        }
    }

    // ===== Utility Functions =====
    function getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    }

    function setLoadingState(formType, isLoading) {
        const btnText = document.getElementById(formType + 'BtnText');
        const loader = document.getElementById(formType + 'Loader');
        const submitBtn = document.querySelector(`#${formType}FormSubmit button[type="submit"]`);

        if (btnText && loader && submitBtn) {
            if (isLoading) {
                const loadingTexts = {
                    login: 'Đang đăng nhập...',
                    register: 'Đang đăng ký...',
                    forgot: 'Đang gửi...',
                    reset: 'Đang xử lý...'
                };
                btnText.textContent = loadingTexts[formType] || 'Đang xử lý...';
                loader.classList.remove('d-none');
                submitBtn.disabled = true;
            } else {
                const defaultTexts = {
                    login: 'Đăng nhập',
                    register: 'Đăng ký',
                    forgot: 'Gửi yêu cầu',
                    reset: 'Đặt lại mật khẩu'
                };
                btnText.textContent = defaultTexts[formType] || 'Gửi';
                loader.classList.add('d-none');
                submitBtn.disabled = false;
            }
        }
    }

    function resetLoadingState(formType) { setLoadingState(formType, false); }
    function fadeOut(el, cb) { el.style.opacity = '0'; setTimeout(() => { if (cb) cb(); }, 300); }
    function fadeIn(el) { el.style.opacity = '0'; setTimeout(() => { el.style.opacity = '1'; }, 50); }
    function isValidEmail(email) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email); }

    function showAlert(message, type) {
        createToast(message, type);
    }

    function createToast(message, type) {
        document.querySelectorAll('.auth-toast').forEach(t => t.remove());
        const container = getOrCreateToastContainer();
        const toast = document.createElement('div');
        toast.className = `auth-toast alert alert-${getBootstrapAlertType(type)} alert-dismissible fade show`;
        toast.style.cssText = `position:fixed;top:20px;right:20px;z-index:9999;min-width:300px;box-shadow:0 4px 12px rgba(0,0,0,0.3);`;
        toast.innerHTML = `${message}<button type="button" class="btn-close" data-bs-dismiss="alert"></button>`;
        container.appendChild(toast);
        setTimeout(() => { toast.remove(); }, 4000);
    }

    function getOrCreateToastContainer() {
        let c = document.getElementById('toast-container');
        if (!c) {
            c = document.createElement('div');
            c.id = 'toast-container';
            c.style.cssText = `position:fixed;top:0;right:0;z-index:9999;padding:20px;`;
            document.body.appendChild(c);
        }
        return c;
    }

    function getBootstrapAlertType(type) {
        switch (type) {
            case 'error': return 'danger';
            case 'success': return 'success';
            case 'warning': return 'warning';
            case 'info': return 'info';
            default: return 'info';
        }
    }
}