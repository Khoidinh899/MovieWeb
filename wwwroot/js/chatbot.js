document.addEventListener('DOMContentLoaded', function() {

    // 1. Lấy các element cần thiết từ HTML
    const fab = document.getElementById('chatbotFloatingBtn');
    const popup = document.getElementById('chatbotPopup');
    const closeBtn = document.getElementById('closeChatbotPopup');
    
    const form = document.getElementById('popupMessageForm');
    const input = document.getElementById('popupMessageInput');
    const messagesList = document.getElementById('popupMessagesList');
    const typingIndicator = document.getElementById('popupTypingIndicator');
    
    const chatBody = document.querySelector('.chatbot-popup-body');

    // --- CÁC HÀM HỖ TRỢ ---

    function scrollToBottom() {
        chatBody.scrollTop = chatBody.scrollHeight;
    }

    function escapeHTML(str) {
        return str.replace(/[&<>"']/g, function(m) {
            return {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            }[m];
        });
    }

    function addMessage(text, sender) {
        const messageElement = document.createElement('li');
        messageElement.classList.add('chat-message', sender);

        // Tin nhắn của AI có thể chứa markdown (xuống dòng), nên ta xử lý nó
        let messageHtml = "";
        if (sender === 'mooner') {
            // Chỉ thay \n bằng <br> và vẫn escape các thẻ khác
            messageHtml = escapeHTML(text).replace(/\n/g, '<br>');
        } else {
            messageHtml = escapeHTML(text);
        }

        messageElement.innerHTML = `<div class="message-bubble">${messageHtml}</div>`;
        messagesList.appendChild(messageElement);
        scrollToBottom();
    }

    // --- CÁC BỘ LẮNG NGHE SỰ KIỆN ---

    fab.addEventListener('click', () => {
        const isHidden = popup.style.display === 'none' || popup.style.display === '';
        
        if (isHidden) {
            popup.style.display = 'flex';
            input.focus();

            if (messagesList.children.length === 0) {
                // TẠM THỜI: Gửi tin nhắn chào mừng từ JS
                // API của bạn sẽ xử lý việc này khi message đầu tiên là rỗng
                setTimeout(() => {
                    addMessage("Xin chào! Tôi là Mooner 🌙. Bạn muốn yêu cầu phim nào?", 'mooner');
                }, 300);
                
                // NÂNG CAO: Gọi API ngay khi mở để lấy tin nhắn chào
                // Bạn có thể kích hoạt form.submit() với 1 tin nhắn đặc biệt
            }
        } else {
            popup.style.display = 'none';
        }
    });

    closeBtn.addEventListener('click', () => {
        popup.style.display = 'none';
    });

    // 3. Sự kiện gửi form (ĐÃ SỬA)
    form.addEventListener('submit', async (event) => { // Thêm 'async'
    event.preventDefault();
    const messageText = input.value.trim();

    if (!messageText) {
        return;
    }

    addMessage(messageText, 'user');
    input.value = '';
    typingIndicator.style.display = 'block';
    scrollToBottom();

    try {
        // *** BƯỚC 1: Đọc token từ HTML (mà bạn đã thêm ở _Layout) ***
        const token = document.getElementById('RequestVerificationToken').value;

        // *** BƯỚC 2: Thêm token vào headers của fetch ***
        const response = await fetch('/api/Chatbot/SendMessage', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token // <-- *** THÊM DÒNG NÀY ***
            },
            body: JSON.stringify({
                message: messageText,
                history: "" 
            })
        });

        // Ẩn "Mooner đang nhập..."
        typingIndicator.style.display = 'none';

        const data = await response.json();

        if (!response.ok) {
            // Lỗi 401 (Unauthorized) hoặc 400 (Bad Request - nếu token sai)
            // sẽ bị bắt ở đây
            addMessage(data.aiMessage || data.error || `Lỗi ${response.status}: Không thể gửi tin nhắn.`, 'mooner');
            return;
        }

        // Nếu OK (200)
        if (data && data.aiMessage) {
            addMessage(data.aiMessage, 'mooner');
        } else {
            addMessage("Xin lỗi, tôi không nhận được phản hồi 😥.", 'mooner');
        }

    } catch (error) {
        // Lỗi mạng hoặc lỗi JavaScript
        console.error('Lỗi khi gọi API chat:', error);
        typingIndicator.style.display = 'none';
        addMessage("Lỗi kết nối 😵. Không thể gọi được API.", 'mooner');
    }
    });
});