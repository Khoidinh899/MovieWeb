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

    let currentMode = 'by_name'; // Default mode
    const modeButtons = document.querySelectorAll('.chatbot-mode-selector .btn-mode');
    
    let conversationHistory = []; // Lưu lịch sử chat

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

    // Hàm addMessage (Cập nhật để render link /phim/slug)
    // isHtml = true -> tin cậy, render link
    // isHtml = false -> mặc định, escape text để bảo mật
    function addMessage(text, sender, isHtml = false) {
        const messageElement = document.createElement('li');
        messageElement.classList.add('chat-message', sender);
        
        let messageHtml;

        if (sender === 'system') {
            messageHtml = `<div class="message-bubble system-bubble">${escapeHTML(text)}</div>`;
        } else {
            if (isHtml) {
                // Tin cậy HTML (dùng cho link /phim/slug do BE trả về)
                messageHtml = text; 
            } else {
                // Mặc định: Xử lý text an toàn
                let safeText = escapeHTML(text);
                safeText = safeText.replace(/\n/g, '<br>');
                // Regex cho link /phim/slug từ mode "recommendation"
                safeText = safeText.replace(/(\/phim\/[^\s<]+)/g, '<a href="$1" style="color: inherit; text-decoration: underline; font-weight: bold;">$1</a>');
                messageHtml = safeText;
            }
            messageHtml = `<div class="message-bubble">${messageHtml}</div>`;
        }

        messageElement.innerHTML = messageHtml;
        messagesList.appendChild(messageElement);
        scrollToBottom();

        // Cập nhật history (chỉ lưu text, không lưu HTML)
        if (sender === 'user' || sender === 'mooner') {
            conversationHistory.push({ role: sender, text: text });
            // Giới hạn 10 tin nhắn
            if (conversationHistory.length > 10) {
                conversationHistory.shift();
            }
        }
    }

    // --- CÁC BỘ LẮNG NGHE SỰ KIỆN ---

    fab.addEventListener('click', () => {
        const isHidden = popup.style.display === 'none' || popup.style.display === '';
        
        if (isHidden) {
            popup.style.display = 'flex';
            input.focus();

            if (messagesList.children.length === 0) {
                setTimeout(() => {
                    const welcomeMessage = "Xin chào! Tôi là Mooner là một trợ lý của MoonPhim🌙\n" +
                                         "Bạn có thể:\n" +
                                         "• Yêu cầu theo tên: Tìm phim bạn đã biết tên\n" +
                                         "• Theo mô tả: Mô tả nội dung, tôi sẽ đoán tên phim\n" +
                                         "• Gợi ý: Tôi sẽ gợi ý phim phù hợp với sở thích của bạn.\n" +
                                         "Tôi sẽ rất vui nếu hoàn thành được những yêu cầu của bạn";
                    addMessage(welcomeMessage, 'mooner');
                }, 300);
            }
        } else {
            popup.style.display = 'none';
        }
    });

    closeBtn.addEventListener('click', () => {
        popup.style.display = 'none';
    });


    // --- Lắng nghe sự kiện cho các nút Mode ---
    modeButtons.forEach(button => {
        button.addEventListener('click', () => {
            const newMode = button.dataset.mode;
            if (newMode === currentMode) return; 

            currentMode = newMode;
            modeButtons.forEach(btn => btn.classList.remove('active'));
            button.classList.add('active');

            let modeText = '';
            if (newMode === 'by_name') modeText = 'Yêu cầu theo tên';
            if (newMode === 'by_description') modeText = 'Theo mô tả';
            if (newMode === 'recommendation') modeText = 'Gợi ý phim';
            
            addMessage(`Đã chuyển sang chế độ "${modeText}" ✨`, 'system');
        });
    });

    // 3. Sự kiện gửi form (ĐÃ SỬA)
    form.addEventListener('submit', async (event) => {
        event.preventDefault();
        const messageText = input.value.trim();

        if (!messageText) return;

        addMessage(messageText, 'user');
        input.value = '';
        typingIndicator.style.display = 'block';
        scrollToBottom();

        try {
            // ========== BẮT ĐẦU THAY ĐỔI JS (GỌI API) ==========

            // BƯỚC 1: Chuẩn bị history
            let conversationHistoryText = conversationHistory
                .map(m => `${m.role}: ${m.text}`)
                .join('\n');
            
            // BƯỚC 2: Gọi fetch (Đã xóa header RequestVerificationToken)
            const response = await fetch('/api/Chatbot/SendMessage', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    // Đã xóa 'RequestVerificationToken'
                },
                body: JSON.stringify({
                    message: messageText,
                    mode: currentMode,
                    history: conversationHistoryText
                })
            });
            
            // ========== KẾT THÚC THAY ĐỔI JS (GỌI API) ==========

            typingIndicator.style.display = 'none';
            const data = await response.json(); // Cố gắng parse JSON

            if (!response.ok) {
                // Lỗi 400, 401, 403, 500 sẽ đi vào đây
                addMessage(data.aiMessage || `Lỗi ${response.status}: Không thể gửi tin nhắn.`, 'mooner');
                return;
            }

            // Nếu OK (200)
            if (data && data.aiMessage) {
                
                // ===== BẮT ĐẦU SỬA Ở ĐÂY =====
                // Kiểm tra xem BE có trả về cờ isHtmlMessage không
                // Nếu có (cho mode "recommendation"), gọi addMessage với isHtml = true
                // Nếu không (cho mode "by_name"), gọi với isHtml = false
                const isHtml = data.isHtmlMessage === true;

                // 1. Thêm tin nhắn AI
                // Nếu là HTML, nó sẽ render button (isHtml = true)
                // Nếu là text, nó sẽ tự escape (isHtml = false)
                addMessage(data.aiMessage, 'mooner', isHtml); 
                
                // 2. Nếu BE trả về movieUrl (mode by_name/by_desc)
                // Logic này vẫn đúng vì nó chỉ chạy khi isHtml = false
                if (data.movieUrl) {
                    const linkHtml = `Bạn có thể xem phim ngay: <a href="${data.movieUrl}" style="color: inherit; text-decoration: underline; font-weight: bold;">Bấm vào đây</a>`;
                    addMessage(linkHtml, 'mooner', true); // true = gửi dưới dạng HTML
                }
                // ===== KẾT THÚC SỬA Ở ĐÂY =====

            } else {
                addMessage("Xin lỗi, tôi không nhận được phản hồi 😥.", 'mooner');
            }

        } catch (error) {
            // Lỗi JavaScript hoặc lỗi mạng (CATCH BLOCK)
            // Lỗi "Unexpected token 'S'" xảy ra ở đây
            console.error('Lỗi khi gọi API chat:', error);
            typingIndicator.style.display = 'none';
            
            // Thêm chi tiết lỗi (error.message) để dễ debug
            addMessage(`Lỗi kết nối 😵. ${error.message}`, 'mooner');
        }
    });
});