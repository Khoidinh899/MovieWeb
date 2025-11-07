let avatarModal;
let verificationModal;
let countdownInterval;

document.addEventListener('DOMContentLoaded', function () {
    avatarModal = new bootstrap.Modal(document.getElementById('avatarSelectionModal'));
    verificationModal = new bootstrap.Modal(document.getElementById('studentVerificationModal'));
    checkStudentEmailStatus();

    const otpInput = document.getElementById('modalOtpCode');
    if (otpInput) {
        otpInput.addEventListener('input', function (e) {
            this.value = this.value.replace(/[^0-9]/g, '');
        });
    }

    // ===================================================
    // START: CODE MỚI THÊM VÀO DOMContentLoaded
    // (Listener cho tab upload avatar)
    // ===================================================
    const uploadInput = document.getElementById('avatarUploadInput');
    const previewImg = document.getElementById('avatarPreview');
    const saveUploadBtn = document.getElementById('saveUploadAvatarBtn');

    // 1. Event listener khi người dùng chọn file (để preview)
    if (uploadInput) {
        uploadInput.addEventListener('change', function () {
            const file = this.files[0];
            if (file) {
                // Kiểm tra có phải là ảnh không
                if (!file.type.startsWith('image/')) {
                    showUploadMessage('Vui lòng chọn một file hình ảnh.', 'danger');
                    this.value = ''; // Reset input
                    previewImg.src = '/images/placeholder-avatar.png'; // Reset về ảnh cũ
                    return;
                }

                // Kiểm tra dung lượng file (ví dụ: 5MB)
                const maxSize = 5 * 1024 * 1024; // 5MB
                if (file.size > maxSize) {
                    showUploadMessage('File quá lớn. Vui lòng chọn file dưới 5MB.', 'danger');
                    this.value = ''; // Reset input
                    previewImg.src = '/images/placeholder-avatar.png';
                    return;
                }

                // Hiển thị preview
                const reader = new FileReader();
                reader.onload = function (e) {
                    previewImg.src = e.target.result;
                };
                reader.readAsDataURL(file);
                showUploadMessage('', 'success', true); // Ẩn message box nếu có
            }
        });
    }

    // 2. Event listener khi nhấn nút "Lưu avatar này"
    if (saveUploadBtn) {
        saveUploadBtn.addEventListener('click', function () {
            uploadAvatar();
        });
    }
    // ===================================================
    // END: CODE MỚI THÊM VÀO DOMContentLoaded
    // ===================================================
});

// Open avatar modal
function openAvatarModal() {
    document.getElementById('avatarMessageBox').style.display = 'none';

    // START: CẢI TIẾN NHỎ (Thêm 1 dòng)
    // Ẩn luôn message box của tab upload (nếu có)
    const uploadMsgBox = document.getElementById('uploadMessageBox');
    if (uploadMsgBox) {
        uploadMsgBox.style.display = 'none';
    }
    // END: CẢI TIẾN NHỎ

    avatarModal.show();
}

// Hàm chọn avatar có sẵn (Giữ nguyên)
async function selectAvatar(avatarName) {
    try {
        // ✅ Lấy CSRF token
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const headers = {
            'Content-Type': 'application/json'
        };

        if (tokenInput) {
            headers['RequestVerificationToken'] = tokenInput.value;
        }

        const response = await fetch('/user/select-avatar', {
            method: 'POST',
            headers: headers,
            body: JSON.stringify({ avatarName: avatarName })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();

        if (result.success) {
            const avatarUrl = result.avatar + '?t=' + new Date().getTime();

            // Update tất cả avatar
            document.getElementById('profileAvatar').src = avatarUrl;
            document.querySelectorAll('.avatar-circle, .avatar-circle-large').forEach(img => {
                img.src = avatarUrl;
            });

            // Hide loading, show success
            showAvatarMessage('✅ Cập nhật avatar thành công!', 'success');

            setTimeout(() => {
                avatarModal.hide();
                location.reload();
            }, 1500);
        } else {
            showAvatarMessage(result.message || '❌ Cập nhật thất bại!', 'danger');
        }
    } catch (error) {
        console.error('Error:', error);
        showAvatarMessage('⚠️ Không thể kết nối server! ' + error.message, 'danger');
    }
}

// Hàm hiển thị message chung (Giữ nguyên)
function showAvatarMessage(message, type) {
    const messageBox = document.getElementById('avatarMessageBox');
    messageBox.className = 'alert alert-' + type + ' mt-3';
    const icon = type === 'success' ? 'check-circle' : 'x-circle';
    messageBox.innerHTML = '<i class="bi bi-' + icon + ' me-2"></i>' + message;
    messageBox.style.display = 'block';
}


// ===================================================
// START: CODE MỚI (CÁC HÀM UPLOAD)
// ===================================================

/**
 * Hàm mới: Xử lý tải file avatar lên server
 */
async function uploadAvatar() {
    const input = document.getElementById('avatarUploadInput');
    const file = input.files[0];
    const saveBtn = document.getElementById('saveUploadAvatarBtn');

    // 1. Validation
    if (!file) {
        showUploadMessage('Vui lòng chọn một file để tải lên.', 'warning');
        return;
    }

    // 2. Lấy CSRF Token (giống hàm selectAvatar)
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const headers = {};
    if (tokenInput) {
        headers['RequestVerificationToken'] = tokenInput.value;
    }

    // 3. Tạo FormData
    const formData = new FormData();
    formData.append('avatarFile', file); // 'avatarFile' phải khớp với tên tham số ở Controller

    // 4. Hiển thị loading trên nút
    saveBtn.disabled = true;
    saveBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Đang tải...';

    try {
        // 5. Gửi Fetch request (đến endpoint mới là /user/upload-avatar)
        const response = await fetch('/user/upload-avatar', {
            method: 'POST',
            headers: headers, // Không cần 'Content-Type', fetch sẽ tự set cho FormData
            body: formData
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();

        if (result.success) {
            // Thành công! Copy logic từ hàm selectAvatar của bạn
            const avatarUrl = result.avatar + '?t=' + new Date().getTime();

            document.getElementById('profileAvatar').src = avatarUrl;
            document.querySelectorAll('.avatar-circle, .avatar-circle-large').forEach(img => {
                img.src = avatarUrl;
            });

            // Hiển thị thông báo thành công ở message box CHUNG (dùng hàm có sẵn của bạn)
            showAvatarMessage('✅ Cập nhật avatar thành công!', 'success');

            // Ẩn message box của tab upload
            showUploadMessage('', 'success', true);

            setTimeout(() => {
                avatarModal.hide();
                location.reload();
            }, 1500);

        } else {
            // Lỗi từ server (ví dụ: file không hợp lệ, lưu thất bại)
            showUploadMessage(result.message || '❌ Cập nhật thất bại!', 'danger');
        }

    } catch (error) {
        console.error('Error uploading avatar:', error);
        showUploadMessage('⚠️ Không thể kết nối server! ' + error.message, 'danger');
    } finally {
        // 6. Trả lại trạng thái bình thường cho nút
        saveBtn.disabled = false;
        saveBtn.innerHTML = '<i class="bi bi-save me-1"></i> Lưu avatar này';
    }
}

/**
 * Hàm mới: Hiển thị thông báo cho tab Upload
 * (Giống hàm showModalMessage của bạn nhưng target vào 'uploadMessageBox')
 */
function showUploadMessage(message, type, hide = false) {
    const messageBox = document.getElementById('uploadMessageBox');
    if (!messageBox) return; // An toàn nếu element không tồn tại

    if (hide) {
        messageBox.style.display = 'none';
        return;
    }
    messageBox.className = 'alert alert-' + type + ' mt-3';
    // Dùng logic icon giống hàm showModalMessage của bạn
    const icon = type === 'success' ? 'check-circle' : (type === 'warning' ? 'exclamation-triangle' : 'x-circle');
    messageBox.innerHTML = '<i class="bi bi-' + icon + ' me-2"></i>' + message;
    messageBox.style.display = 'block';
}

// ===================================================
// END: CODE MỚI
// ===================================================


// Check student email verification status (Giữ nguyên)
function checkStudentEmailStatus(isVerified, verifiedDateStr) {
    
    // Dùng 2 tham số này thay vì Model
    if (isVerified && verifiedDateStr)
    {
        // <text> không còn cần thiết nữa
        const verifiedDate = new Date(verifiedDateStr);
        const now = new Date();
        const daysDiff = Math.floor((now - verifiedDate) / (1000 * 60 * 60 * 24));

        if (daysDiff > 365) {
            document.getElementById('studentVerifiedBadge').style.display = 'none';
            document.getElementById('studentExpiredBadge').style.display = 'inline-block';
            document.getElementById('reverifyStudentBtn').style.display = 'inline-block';
        } else if (daysDiff >= 358) {
            document.getElementById('studentExpiringSoonBadge').style.display = 'inline-block';
            document.getElementById('renewStudentBtn').style.display = 'inline-block';
        }
        // </text> không còn cần thiết nữa
    }
}

// Open verification modal (Giữ nguyên)
function openVerificationModal() {
    document.getElementById('modalStep1').style.display = 'block';
    document.getElementById('modalStep2').style.display = 'none';
    document.getElementById('modalOtpCode').value = '';
    document.getElementById('modalMessageBox').style.display = 'none';
    if (countdownInterval) {
        clearInterval(countdownInterval);
    }
    verificationModal.show();
}

// Gửi OTP (Giữ nguyên)
async function sendOTP() {
    const email = document.getElementById('modalStudentEmail').value.trim();
    if (!email) {
        showModalMessage('Vui lòng nhập email sinh viên!', 'danger');
        return;
    }

    const studentEmailPattern = /^[^\s@]+@[^\s@]+\.(edu|edu\.vn|ac\.vn)$/i;
    if (!studentEmailPattern.test(email)) {
        showModalMessage('Email phải có đuôi .edu, .edu.vn hoặc .ac.vn', 'danger');
        return;
    }
    showModalLoading(true);
    disableModalButton('modalSendOtpBtn', true);

    try {
        const response = await fetch('/api/profile/send-student-otp', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ studentEmail: email })
        });

        let result;
        const text = await response.text();
        try {
            result = text ? JSON.parse(text) : {};
        } catch (e) {
            result = { isSuccess: false, message: 'Server trả về dữ liệu không hợp lệ' };
        }

        if (result.isSuccess) {
            document.getElementById('modalStep1').style.display = 'none';
            document.getElementById('modalStep2').style.display = 'block';
            document.getElementById('modalSentEmailDisplay').textContent = email;
            startCountdown(300);
            showModalMessage('Mã OTP đã được gửi đến email của bạn!', 'success');

            setTimeout(() => {
                document.getElementById('modalOtpCode').focus();
            }, 300);
        } else {
            showModalMessage(result.message || 'Email đã được sử dụng, vui lòng nhập email khác!', 'danger');
        }
    } catch (error) {
        console.error('Error:', error);
        showModalMessage('Không thể kết nối đến server!', 'danger');
    } finally {
        showModalLoading(false);
        disableModalButton('modalSendOtpBtn', false);
    }
}

// Xác thực OTP (Giữ nguyên)
async function verifyOTP() {
    const otp = document.getElementById('modalOtpCode').value.trim();

    if (!otp || otp.length !== 6) {
        showModalMessage('Vui lòng nhập đủ 6 chữ số OTP!', 'danger');
        return;
    }

    showModalLoading(true);
    disableModalButton('modalVerifyOtpBtn', true);

    try {
        const response = await fetch('/api/profile/verify-student-email', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ otpCode: otp })
        });

        let result;
        const text = await response.text();
        try {
            result = text ? JSON.parse(text) : {};
        } catch (e) {
            result = { isSuccess: false, message: 'Server trả về dữ liệu không hợp lệ' };
        }

        if (result.isSuccess) {
            showModalMessage('Xác thực email sinh viên thành công!', 'success');
            if (countdownInterval) {
                clearInterval(countdownInterval);
            }
            setTimeout(() => {
                verificationModal.hide();
                window.location.reload();
            }, 2000);
        } else {
            showModalMessage(result.message || 'Mã OTP không đúng hoặc đã hết hạn!', 'danger');
        }
    } catch (error) {
        console.error('Error:', error);
        showModalMessage('Không thể kết nối đến server!', 'danger');
    } finally {
        showModalLoading(false);
        disableModalButton('modalVerifyOtpBtn', false);
    }
}

// Resend OTP (Giữ nguyên)
async function resendOTP() {
    if (countdownInterval) {
        clearInterval(countdownInterval);
    }
    document.getElementById('modalOtpCode').value = '';
    document.getElementById('modalStep2').style.display = 'none';
    document.getElementById('modalStep1').style.display = 'block';
    await sendOTP();
}

// Cancel verification (Giữ nguyên)
function cancelVerification() {
    if (countdownInterval) {
        clearInterval(countdownInterval);
    }
    document.getElementById('modalStep2').style.display = 'none';
    document.getElementById('modalStep1').style.display = 'block';
    document.getElementById('modalOtpCode').value = '';
    document.getElementById('modalMessageBox').style.display = 'none';
}

// Start countdown (Giữ nguyên)
function startCountdown(seconds) {
    let timeLeft = seconds;
    const countdownElement = document.getElementById('modalCountdown');

    countdownInterval = setInterval(() => {
        const minutes = Math.floor(timeLeft / 60);
        const secs = timeLeft % 60;
        countdownElement.textContent = minutes.toString().padStart(2, '0') + ':' + secs.toString().padStart(2, '0');

        if (timeLeft <= 0) {
            clearInterval(countdownInterval);
            showModalMessage('Mã OTP đã hết hạn. Vui lòng gửi lại mã mới!', 'warning');
            disableModalButton('modalVerifyOtpBtn', true);
        }
        timeLeft--;
    }, 1000);
}

// Show modal loading (Giữ nguyên)
function showModalLoading(show) {
    document.getElementById('modalLoadingSpinner').style.display = show ? 'block' : 'none';
    document.getElementById('modalStep1').style.display = show ? 'none' : (document.getElementById('modalStep2').style.display === 'none' ? 'block' : 'none');
    document.getElementById('modalStep2').style.display = show ? 'none' : document.getElementById('modalStep2').style.display;
}

// Disable modal button (Giữ nguyên)
function disableModalButton(buttonId, disable) {
    const button = document.getElementById(buttonId);
    if (button) {
        button.disabled = disable;
    }
}

// Show modal message (Giữ nguyên)
function showModalMessage(message, type) {
    const messageBox = document.getElementById('modalMessageBox');
    messageBox.className = 'alert alert-' + type + ' mt-3';
    const icon = type === 'success' ? 'check-circle' : (type === 'warning' ? 'exclamation-triangle' : 'x-circle');
    messageBox.innerHTML = '<i class="bi bi-' + icon + ' me-2"></i>' + message;
    messageBox.style.display = 'block';

    if (type === 'success') {
        setTimeout(() => {
            messageBox.style.display = 'none';
        }, 5000);
    }
}

// Open renew modal (GiVới nguyên)
function openRenewModal() {
    if (confirm('Bạn muốn gia hạn xác thực email sinh viên?')) {
        openVerificationModal();
    }
}