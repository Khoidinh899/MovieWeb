document.addEventListener('DOMContentLoaded', function () {
    initializeAuth();
});

function initializeAuth() {
    const authModal = new bootstrap.Modal(document.getElementById('authModal'));
    const loginBtn = document.getElementById('loginBtn');
    const mobileLoginBtn = document.getElementById('mobileLoginBtn');
    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');
    const verificationSuccess = document.getElementById('verificationSuccess');

    setupFormToggles();
    setupModalTriggers();
    setupFormSubmissions();
    setupLogout();

    // ===== Form Toggle Functions =====
    function setupFormToggles() {
        document.getElementById('showRegister').addEventListener('click', e => {
            e.preventDefault();
            switchToRegister();
        });

        document.getElementById('showLogin').addEventListener('click', e => {
            e.preventDefault();
            switchToLogin();
        });

        document.getElementById('backToLogin').addEventListener('click', e => {
            e.preventDefault();
            switchToLogin();
        });
    }

    function switchToRegister() {
        fadeOut(loginForm, () => {
            loginForm.classList.add('d-none');
            registerForm.classList.remove('d-none');
            document.querySelector('.modal-title').textContent = 'Đăng ký';
            fadeIn(registerForm);
        });
    }

    function switchToLogin() {
        const activeForm = registerForm.classList.contains('d-none') ? verificationSuccess : registerForm;

        fadeOut(activeForm, () => {
            registerForm.classList.add('d-none');
            verificationSuccess.classList.add('d-none');
            loginForm.classList.remove('d-none');
            document.querySelector('.modal-title').textContent = 'Đăng nhập';
            document.getElementById('emailVerificationAlert')?.classList.add('d-none');
            fadeIn(loginForm);
        });
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
        document.getElementById('loginFormSubmit').reset();
        document.getElementById('registerFormSubmit').reset();
        loginForm.classList.remove('d-none');
        registerForm.classList.add('d-none');
        verificationSuccess.classList.add('d-none');
        document.querySelector('.modal-title').textContent = 'Đăng nhập';
        document.getElementById('emailVerificationAlert')?.classList.add('d-none');
        resetLoadingState('login');
        resetLoadingState('register');
    }

    // ===== Form Submission Functions =====
    function setupFormSubmissions() {
        document.getElementById('loginFormSubmit').addEventListener('submit', async e => {
            e.preventDefault();
            await handleLogin();
        });

        document.getElementById('registerFormSubmit').addEventListener('submit', async e => {
            e.preventDefault();
            await handleRegister();
        });

        document.getElementById('resendVerification')?.addEventListener('click', async e => {
            e.preventDefault();
            showAlert('Chức năng gửi lại email xác thực chưa được hỗ trợ.', 'info');
        });

        document.getElementById('forgotPassword')?.addEventListener('click', e => {
            e.preventDefault();
            handleForgotPassword();
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

                // Redirect và reload để server render lại navbar với user info
                setTimeout(() => {
                    window.location.href = result.redirectUrl;
                }, 500);
            } else {
                showAlert(result.message, 'danger');
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
        setLoadingState('register', true);

        try {
            const response = await fetch('/Auth/Register', {
                method: 'POST',
                body: formData
            });

            if (response.redirected) {
                fadeOut(registerForm, () => {
                    registerForm.classList.add('d-none');
                    verificationSuccess.classList.remove('d-none');
                    document.querySelector('.modal-title').textContent = 'Đăng ký thành công';
                    fadeIn(verificationSuccess);
                });
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

    function handleForgotPassword() {
        const email = document.getElementById('loginEmail').value.trim();
        if (!email) {
            showAlert('Vui lòng nhập email trước khi click "Quên mật khẩu"', 'warning');
            return;
        }
        if (!isValidEmail(email)) {
            showAlert('Email không hợp lệ', 'warning');
            return;
        }

        const formData = new FormData();
        formData.append('Email', email);

        fetch('/Auth/ForgotPassword', {
            method: 'POST',
            body: formData
        })
            .then(response => {
                if (response.ok) {
                    showAlert('Nếu email tồn tại, link đặt lại mật khẩu đã được gửi.', 'success');
                } else {
                    showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
                }
            })
            .catch(error => {
                console.error('Forgot password error:', error);
                showAlert('Có lỗi xảy ra khi gửi yêu cầu đặt lại mật khẩu.', 'danger');
            });
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
            const response = await fetch('/Auth/Logout', {
                method: 'POST',
                headers: { 'RequestVerificationToken': getAntiForgeryToken() }
            });

            if (response.ok) {
                // XÓA DÒNG NÀY: updateUIForLoggedOutUser();
                showAlert('Đăng xuất thành công!', 'success');
                // THÊM DÒNG NÀY: Reload trang để server render lại
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
    // ===== Utility =====
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
                btnText.textContent = formType === 'login' ? 'Đang đăng nhập...' : 'Đang đăng ký...';
                loader.classList.remove('d-none');
                submitBtn.disabled = true;
            } else {
                btnText.textContent = formType === 'login' ? 'Đăng nhập' : 'Đăng ký';
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