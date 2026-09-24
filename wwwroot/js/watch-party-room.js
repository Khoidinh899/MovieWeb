/**
 * 🌙 MOONPHIM - WATCH PARTY ROOM CLIENT ENGINE
 * SignalR Realtime Synchronization, Danmaku & Chat
 */

document.addEventListener('DOMContentLoaded', () => {
    const config = window.WP_CONFIG || {};
    if (!config.roomCode) return;

    // Elements
    const video = document.getElementById('wpVideoPlayer');
    const embedPlayer = document.getElementById('wpEmbedPlayer');
    const danmakuContainer = document.getElementById('wpDanmakuContainer');
    const chatInput = document.getElementById('wpChatInput');
    const btnSendChat = document.getElementById('wpBtnSendChat');
    const messagesContainer = document.getElementById('wpMessagesContainer');
    const danmakuInput = document.getElementById('wpDanmakuInput');
    const danmakuColor = document.getElementById('wpDanmakuColor');
    const btnSendDanmaku = document.getElementById('wpBtnSendDanmaku');
    const btnToggleDanmaku = document.getElementById('wpBtnToggleDanmaku');
    const memberCountEl = document.getElementById('wpMemberCount');
    const membersListEl = document.getElementById('wpMembersList');
    const graceBanner = document.getElementById('wpGraceBanner');
    const graceCountdownEl = document.getElementById('wpGraceCountdown');

    // Dock Controls Elements
    const btnPlayPause = document.getElementById('wpBtnPlayPause');
    const btnPlayPauseIcon = document.getElementById('wpBtnPlayPauseIcon');
    const timelineSlider = document.getElementById('wpTimelineSlider');
    const currentTimeText = document.getElementById('wpCurrentTimeText');
    const totalDurationText = document.getElementById('wpTotalDurationText');

    // State Flags
    let currentHls = null;
    let danmaku = null;
    let graceTimer = null;
    let currentVideoUrl = config.videoUrl || '';
    let isDraggingTimeline = false;

    let roomState = {
        isHost: config.isHost,
        hostUserId: config.hostUserId,
        onlyHostControl: config.onlyHostControl,
        allowDanmaku: config.allowDanmaku !== false,
        isPlaying: false,
        currentTime: 0
    };

    // Initialize Danmaku Engine
    if (danmakuContainer) {
        danmaku = new DanmakuEngine(danmakuContainer);
    }

    // ==========================================
    // 🔔 TOAST NOTIFICATION HELPER
    // ==========================================
    function showToast(message, type = 'info') {
        let container = document.getElementById('wpToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'wpToastContainer';
            container.className = 'wp-toast-container';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = `wp-toast toast-${type}`;

        const iconMap = {
            'success': 'fa-circle-check text-success',
            'danger': 'fa-circle-exclamation text-danger',
            'warning': 'fa-triangle-exclamation text-warning',
            'info': 'fa-circle-info text-info'
        };

        const icon = iconMap[type] || 'fa-circle-info text-info';
        toast.innerHTML = `<i class="fa-solid ${icon}"></i><span>${escapeHtml(message)}</span>`;
        container.appendChild(toast);

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateY(20px)';
            setTimeout(() => toast.remove(), 300);
        }, 3500);
    }

    // ==========================================
    // ⏱️ TIME FORMATTER & UI HELPERS
    // ==========================================
    function formatTime(seconds) {
        if (isNaN(seconds) || seconds < 0) return '00:00';
        const s = Math.floor(seconds);
        const hrs = Math.floor(s / 3600);
        const mins = Math.floor((s % 3600) / 60);
        const secs = s % 60;
        const pad = (n) => (n < 10 ? '0' + n : n);
        if (hrs > 0) {
            return `${pad(hrs)}:${pad(mins)}:${pad(secs)}`;
        }
        return `${pad(mins)}:${pad(secs)}`;
    }

    function updatePlayPauseButtonUI() {
        if (!btnPlayPauseIcon) return;
        if (roomState.isPlaying) {
            btnPlayPauseIcon.className = 'fa-solid fa-pause';
            if (btnPlayPause) btnPlayPause.title = 'Tạm dừng toàn phòng';
        } else {
            btnPlayPauseIcon.className = 'fa-solid fa-play';
            if (btnPlayPause) btnPlayPause.title = 'Phát phim toàn phòng';
        }
    }

    function updateTimelineUI() {
        if (timelineSlider && !isDraggingTimeline) {
            timelineSlider.value = Math.floor(roomState.currentTime);
        }
        if (currentTimeText) {
            currentTimeText.textContent = formatTime(roomState.currentTime);
        }
    }

    // 1-second interval ticker for timeline slider & time display
    setInterval(() => {
        if (roomState.isPlaying) {
            roomState.currentTime += 1;
            updateTimelineUI();
        }
    }, 1000);

    // ==========================================
    // 🎬 VIDEO & IFRAME EMBED LOADER
    // ==========================================
    function buildEmbedUrl(url, startTime) {
        if (!url) return '';
        let target = url.trim();
        // Remove old timestamp query or hash
        target = target.replace(/[?&]t=\d+/g, '').replace(/#t=\d+/g, '');
        const startSec = Math.floor(startTime || 0);
        if (startSec > 0) {
            const sep = target.includes('?') ? '&' : '?';
            target = `${target}${sep}t=${startSec}#t=${startSec}`;
        }
        return target;
    }

    function loadVideo(url, startTime = 0, autoPlay = false) {
        if (!url || !url.trim()) return;
        currentVideoUrl = url.trim();

        if (currentHls) {
            currentHls.destroy();
            currentHls = null;
        }
        if (video) {
            video.pause();
            video.removeAttribute('src');
            video.style.display = 'none';
        }
        if (embedPlayer) {
            embedPlayer.style.display = 'block';
            const finalUrl = buildEmbedUrl(currentVideoUrl, startTime);
            if (embedPlayer.src !== finalUrl) {
                embedPlayer.src = finalUrl;
            }
        }

        roomState.currentTime = startTime || 0;
        updateTimelineUI();
        updatePlayPauseButtonUI();
    }

    // ==========================================
    // 1️⃣ SIGNALR CONNECTION
    // ==========================================
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/watchPartyHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // SignalR Handlers
    connection.on("OnInitialState", (state) => {
        roomState.isHost = state.isHost;
        roomState.hostUserId = state.hostUserId;
        roomState.onlyHostControl = state.onlyHostControl;
        roomState.allowDanmaku = (state.allowDanmaku !== false);
        roomState.isPlaying = state.isPlaying;
        roomState.currentTime = state.currentTime || 0;
        
        updateHostControlsUI();
        updateDanmakuUI();
        updatePlayPauseButtonUI();
        updateTimelineUI();

        if (state.messages && state.messages.length > 0) {
            messagesContainer.innerHTML = '';
            state.messages.forEach(msg => appendMessage(msg));
        }

        if (state.members) {
            updateMemberList(state.members);
        }

        // Khởi tạo Video Iframe Embed với currentTime được server tính toán trực tiếp cho người vào sau
        if (config.videoUrl) {
            loadVideo(config.videoUrl, roomState.currentTime, roomState.isPlaying);
        }
    });

    connection.on("OnSyncPlay", (data) => {
        roomState.isPlaying = true;
        roomState.currentTime = data.currentTime;
        updatePlayPauseButtonUI();
        updateTimelineUI();

        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tiếp tục phát`, '#60a5fa', 'top');
        }
        showToast(`${data.senderName} đã tiếp tục phát`, 'info');
    });

    connection.on("OnSyncPause", (data) => {
        roomState.isPlaying = false;
        roomState.currentTime = data.currentTime;
        updatePlayPauseButtonUI();
        updateTimelineUI();

        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tạm dừng`, '#fca5a5', 'top');
        }
        showToast(`${data.senderName} đã tạm dừng`, 'info');
    });

    connection.on("OnSyncSeek", (data) => {
        roomState.currentTime = data.currentTime;
        updateTimelineUI();

        // Cập nhật lại Iframe nhúng về mốc tua mới
        if (currentVideoUrl && embedPlayer) {
            const finalUrl = buildEmbedUrl(currentVideoUrl, data.currentTime);
            embedPlayer.src = finalUrl;
        }

        const timeStr = formatTime(data.currentTime);
        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tua đến ${timeStr}`, '#fbbf24', 'top');
        }
        showToast(`${data.senderName} đã tua đến ${timeStr}`, 'info');
    });

    connection.on("OnSyncHeartbeat", (data) => {
        if (roomState.isHost) return;
        roomState.isPlaying = data.isPlaying;
        updatePlayPauseButtonUI();
        if (Math.abs(roomState.currentTime - data.currentTime) > 5) {
            roomState.currentTime = data.currentTime;
            updateTimelineUI();
        }
    });

    connection.on("OnEpisodeChanged", (data) => {
        appendSystemMessage(`Chủ phòng đã chuyển sang Tập ${data.episodeNumber} (${data.serverName})`);
        currentVideoUrl = data.videoUrl;
        roomState.currentTime = 0;
        roomState.isPlaying = true;
        updatePlayPauseButtonUI();
        updateTimelineUI();

        if (data.videoUrl) {
            loadVideo(data.videoUrl, 0, true);
        }
        
        // Highlight active episode button
        document.querySelectorAll('.wp-ep-item').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.episodeId == data.episodeId);
        });

        showToast(`Đã chuyển sang Tập ${data.episodeNumber}`, "success");
    });

    connection.on("OnReceiveChatMessage", (msg) => {
        appendMessage(msg);
    });

    connection.on("OnReceiveDanmaku", (dto) => {
        if (danmaku) {
            danmaku.emit(dto.text, dto.color, dto.position);
        }
    });

    connection.on("OnUserJoined", (data) => {
        appendSystemMessage(`${data.userName} đã tham gia phòng.`);
        if (memberCountEl) memberCountEl.textContent = data.memberCount;
        if (data.members) updateMemberList(data.members);
    });

    connection.on("OnUserLeft", (data) => {
        appendSystemMessage(`${data.userName} đã rời phòng.`);
        if (memberCountEl) memberCountEl.textContent = data.memberCount;
        const memberItem = document.getElementById(`member-card-${data.userId}`);
        if (memberItem) memberItem.remove();
    });

    connection.on("OnHostDisconnected", (data) => {
        if (graceBanner) {
            graceBanner.style.display = 'flex';
            let remain = data.graceSeconds || 120;
            if (graceCountdownEl) graceCountdownEl.textContent = `${remain}s`;

            if (graceTimer) clearInterval(graceTimer);
            graceTimer = setInterval(() => {
                remain--;
                if (graceCountdownEl) graceCountdownEl.textContent = `${remain}s`;
                if (remain <= 0) {
                    clearInterval(graceTimer);
                }
            }, 1000);
        }
    });

    connection.on("OnHostReconnected", (data) => {
        if (graceBanner) graceBanner.style.display = 'none';
        if (graceTimer) clearInterval(graceTimer);
        appendSystemMessage(data.message);
        if (danmaku) danmaku.emit("👑 Chủ phòng đã quay trở lại!", "#fbbf24", "top");
    });

    connection.on("OnHostTransferred", (data) => {
        if (graceBanner) graceBanner.style.display = 'none';
        if (graceTimer) clearInterval(graceTimer);

        roomState.hostUserId = data.newHostUserId;
        roomState.isHost = (config.currentUserId == data.newHostUserId);
        
        updateHostControlsUI();
        appendSystemMessage(data.message || `Quyền chủ phòng đã được chuyển cho ${data.newHostName}!`);
        if (danmaku) danmaku.emit(`👑 ${data.newHostName} là chủ phòng mới`, "#fbbf24", "top");
    });

    connection.on("OnSettingsUpdated", (data) => {
        roomState.onlyHostControl = data.onlyHostControl;
        roomState.allowDanmaku = (data.allowDanmaku !== false);
        
        updateHostControlsUI();
        updateDanmakuUI();

        const controlText = data.onlyHostControl ? "Chỉ chủ phòng điều khiển video" : "Mọi người có thể điều khiển video";
        const danmakuText = (data.allowDanmaku !== false) ? "Đã bật bình luận bay (Danmaku)" : "Đã tắt bình luận bay (Danmaku)";
        
        showToast(`${controlText} • ${danmakuText}`, "info");
    });

    connection.on("OnRoomClosed", async (msg) => {
        showToast(msg || "Chủ phòng đã kết thúc phiên xem chung.", "warning");
        if (window.MoonDialog) {
            window.MoonDialog.alert({
                title: 'Phòng đã kết thúc',
                message: msg || 'Chủ phòng đã kết thúc phiên xem chung. Đang chuyển về sảnh xem chung...',
                type: 'warning',
                iconClass: 'bi-door-closed-fill',
                btnText: 'Về sảnh ngay'
            }).then(() => {
                window.location.href = "/watch-party";
            });
            setTimeout(() => {
                window.location.href = "/watch-party";
            }, 2500);
        } else {
            setTimeout(() => {
                window.location.href = "/watch-party";
            }, 1500);
        }
    });

    connection.on("OnError", (err) => {
        showToast(err, "danger");
    });

    // Start Connection
    async function startConnection() {
        try {
            await connection.start();
            await connection.invoke("JoinRoom", config.roomCode, config.shareToken, null);
        } catch (err) {
            console.error("SignalR Connection Error:", err);
            setTimeout(startConnection, 3000);
        }
    }

    startConnection();

    // Host Periodic Playback Heartbeat (every 5 seconds while playing)
    setInterval(() => {
        if (roomState.isHost && roomState.isPlaying && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("SyncHeartbeat", config.roomCode, roomState.currentTime).catch(() => {});
        }
    }, 5000);

    // Eagerly initialize video on DOM Load so player does not appear blank
    if (config.videoUrl) {
        loadVideo(config.videoUrl, 0, false);
    }

    // ==========================================
    // 2️⃣ DOCK CONTROLS (PLAY/PAUSE/SEEK/SYNC)
    // ==========================================
    window.togglePlayPause = function () {
        if (roomState.onlyHostControl && !roomState.isHost) {
            showToast("Chỉ chủ phòng mới có quyền điều khiển phát video.", "warning");
            return;
        }
        if (roomState.isPlaying) {
            connection.invoke("SyncPause", config.roomCode, roomState.currentTime).catch(console.error);
        } else {
            connection.invoke("SyncPlay", config.roomCode, roomState.currentTime).catch(console.error);
        }
    };

    window.quickSeek = function (deltaSeconds) {
        if (roomState.onlyHostControl && !roomState.isHost) {
            showToast("Chỉ chủ phòng mới có quyền tua video.", "warning");
            return;
        }
        const newTime = Math.max(0, roomState.currentTime + deltaSeconds);
        roomState.currentTime = newTime;
        updateTimelineUI();
        connection.invoke("SyncSeek", config.roomCode, newTime).catch(console.error);
    };

    window.onTimelineInput = function (slider) {
        isDraggingTimeline = true;
        if (currentTimeText) {
            currentTimeText.textContent = formatTime(slider.value);
        }
    };

    window.onTimelineChange = function (slider) {
        isDraggingTimeline = false;
        if (roomState.onlyHostControl && !roomState.isHost) {
            showToast("Chỉ chủ phòng mới có quyền tua video.", "warning");
            slider.value = Math.floor(roomState.currentTime);
            if (currentTimeText) currentTimeText.textContent = formatTime(roomState.currentTime);
            return;
        }
        const newTime = Math.max(0, parseFloat(slider.value) || 0);
        roomState.currentTime = newTime;
        updateTimelineUI();
        connection.invoke("SyncSeek", config.roomCode, newTime).catch(console.error);
    };

    window.syncWithHost = function () {
        if (!currentVideoUrl) return;
        loadVideo(currentVideoUrl, roomState.currentTime, roomState.isPlaying);
        showToast(`Đã đồng bộ video ở mốc ${formatTime(roomState.currentTime)}!`, "success");
        if (danmaku) {
            danmaku.emit(`🔄 Đã đồng bộ video với phòng (${formatTime(roomState.currentTime)})`, '#60a5fa', 'top');
        }
    };

    // ==========================================
    // 3️⃣ CHAT & DANMAKU SENDING
    // ==========================================
    function sendChat() {
        if (!chatInput) return;
        const text = chatInput.value.trim();
        if (!text) return;

        connection.invoke("SendChatMessage", config.roomCode, text).catch(console.error);
        chatInput.value = '';
    }

    function sendDanmaku() {
        if (!roomState.allowDanmaku) {
            showToast("Chủ phòng đã tắt tính năng bình luận bay trong phòng này.", "warning");
            return;
        }
        if (!danmakuInput) return;
        const text = danmakuInput.value.trim();
        if (!text) return;

        const color = danmakuColor ? danmakuColor.value : '#ffffff';
        const currentTime = roomState.currentTime || 0;

        connection.invoke("SendDanmaku", config.roomCode, text, color, "scroll", currentTime).catch(err => {
            console.error(err);
            showToast("Không thể gửi bình luận bay", "danger");
        });
        danmakuInput.value = '';
    }

    if (btnSendChat) btnSendChat.addEventListener('click', sendChat);
    if (chatInput) {
        chatInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                sendChat();
            }
        });
    }

    if (btnSendDanmaku) btnSendDanmaku.addEventListener('click', sendDanmaku);
    if (danmakuInput) {
        danmakuInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                sendDanmaku();
            }
        });
    }

    // 🎨 4-Pastel Color Palette Selector
    const colorBadge = document.getElementById('wpDanmakuColorBadge');
    const colorPopover = document.getElementById('wpColorPalettePopover');
    const colorOptions = document.querySelectorAll('.wp-color-option');

    if (colorBadge && colorPopover) {
        colorBadge.addEventListener('click', (e) => {
            e.stopPropagation();
            colorPopover.classList.toggle('show');
        });

        document.addEventListener('click', (e) => {
            if (!e.target.closest('#wpColorPickerDropdown')) {
                colorPopover.classList.remove('show');
            }
        });

        colorOptions.forEach(opt => {
            opt.addEventListener('click', (e) => {
                e.stopPropagation();
                const selectedColor = opt.dataset.color || '#ffffff';
                if (danmakuColor) danmakuColor.value = selectedColor;
                
                colorOptions.forEach(o => o.classList.remove('active'));
                opt.classList.add('active');

                colorBadge.style.color = selectedColor;
                colorBadge.style.boxShadow = `0 0 12px ${selectedColor}88`;
                colorPopover.classList.remove('show');
            });
        });

        const defaultColor = danmakuColor ? danmakuColor.value : '#ffffff';
        colorBadge.style.color = defaultColor;
    }

    if (btnToggleDanmaku && danmaku) {
        btnToggleDanmaku.addEventListener('click', () => {
            const isEnabled = danmaku.toggle();
            btnToggleDanmaku.classList.toggle('active', isEnabled);
            btnToggleDanmaku.title = isEnabled ? "Tắt bình luận bay (C)" : "Bật bình luận bay (C)";
        });
    }

    // Keybindings (C for danmaku, Enter for chat)
    document.addEventListener('keydown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') {
            if (e.key === 'Escape') e.target.blur();
            return;
        }

        if (e.key === 'c' || e.key === 'C') {
            if (danmaku && btnToggleDanmaku) {
                btnToggleDanmaku.click();
            }
        } else if (e.key === 'Enter') {
            e.preventDefault();
            if (danmakuInput && !danmakuInput.disabled) danmakuInput.focus();
            else if (chatInput) chatInput.focus();
        }
    });

    // Helper functions for Chat UI
    function appendMessage(msg) {
        if (!messagesContainer) return;
        const div = document.createElement('div');
        div.className = 'wp-message-item';

        const avatar = msg.avatarUrl || '/images/nouser.png';
        const time = msg.timestamp ? new Date(msg.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';

        div.innerHTML = `
            <img src="${avatar}" class="wp-message-avatar" onerror="this.src='/images/nouser.png';" alt="Avatar" />
            <div class="wp-message-content">
                <div class="wp-message-header">
                    <span class="wp-sender-name ${msg.isHost ? 'host' : ''}">${escapeHtml(msg.userName)}</span>
                    ${msg.isHost ? '<span class="wp-host-badge">Host</span>' : ''}
                    <span class="wp-message-time">${time}</span>
                </div>
                <div class="wp-message-bubble">${escapeHtml(msg.message)}</div>
            </div>
        `;

        messagesContainer.appendChild(div);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    function appendSystemMessage(text) {
        if (!messagesContainer) return;
        const div = document.createElement('div');
        div.className = 'wp-system-message';
        div.textContent = text;
        messagesContainer.appendChild(div);
        messagesContainer.scrollTop = messagesContainer.scrollHeight;
    }

    function updateMemberList(members) {
        if (!membersListEl) return;
        membersListEl.innerHTML = '';
        members.forEach(m => {
            const card = document.createElement('div');
            card.className = 'wp-member-card';
            card.id = `member-card-${m.userId}`;
            card.innerHTML = `
                <div class="wp-member-meta">
                    <img src="${m.avatarUrl || '/images/nouser.png'}" class="wp-member-avatar" onerror="this.src='/images/nouser.png';" />
                    <div>
                        <span class="wp-member-name">${escapeHtml(m.userName)}</span>
                        ${m.isHost ? ' <span class="wp-host-badge">Host</span>' : ''}
                    </div>
                </div>
                ${roomState.isHost && !m.isHost ? `<button class="wp-icon-btn btn-sm" onclick="window.transferHostTo(${m.userId})">Chuyển Host</button>` : ''}
            `;
            membersListEl.appendChild(card);
        });
    }

    function updateHostControlsUI() {
        const hostOnlyEls = document.querySelectorAll('.wp-host-only');
        hostOnlyEls.forEach(el => {
            el.style.display = roomState.isHost ? '' : 'none';
        });
    }

    function updateDanmakuUI() {
        if (!danmakuInput) return;
        if (roomState.allowDanmaku) {
            danmakuInput.disabled = false;
            danmakuInput.placeholder = "Gửi bình luận bay lên màn hình (Phím Enter)...";
            if (btnSendDanmaku) btnSendDanmaku.disabled = false;
        } else {
            danmakuInput.disabled = true;
            danmakuInput.placeholder = "Chủ phòng đã tắt tính năng bình luận bay trong phòng.";
            if (btnSendDanmaku) btnSendDanmaku.disabled = true;
        }
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ==========================================
    // 4️⃣ HOST ACTIONS (GLOBAL WINDOW METHODS)
    // ==========================================
    window.onEpisodeClick = function (el) {
        if (!el) return;
        const epId = el.dataset.episodeId;
        const epName = el.dataset.episodeName;
        const serverTitle = el.dataset.serverTitle;
        const linkM3u8 = el.dataset.linkM3u8 || '';
        window.changeEpisode(epId, epName, serverTitle, linkM3u8);
    };

    window.changeEpisode = function (episodeId, episodeNumber, serverName, videoUrl) {
        if (!roomState.isHost) {
            showToast("Chỉ chủ phòng mới có quyền đổi tập phim.", "warning");
            return;
        }

        const match = (episodeNumber || '').toString().match(/\d+/);
        const epNum = match ? parseInt(match[0]) : (parseInt(episodeNumber) || 1);
        const epId = parseInt(episodeId) || 0;

        showToast(`Đang chuyển sang Tập ${episodeNumber}...`, "info");
        connection.invoke("ChangeEpisode", config.roomCode, epId, epNum, serverName || '', videoUrl || '')
            .catch(err => {
                console.error("ChangeEpisode error:", err);
                showToast("Lỗi khi đổi tập phim: " + (err.message || err), "danger");
            });
    };

    window.transferHostTo = async function (targetUserId) {
        const isConfirmed = window.MoonDialog ? await window.MoonDialog.confirm({
            title: 'Chuyển quyền chủ phòng',
            message: 'Bạn có chắc chắn muốn chuyển quyền chủ phòng cho thành viên này không?',
            confirmText: 'Chuyển quyền',
            cancelText: 'Hủy',
            type: 'warning',
            iconClass: 'bi-person-gear'
        }) : confirm("Bạn có chắc chắn muốn chuyển quyền chủ phòng cho thành viên này?");

        if (!isConfirmed) return;

        connection.invoke("TransferHost", config.roomCode, parseInt(targetUserId)).catch(err => {
            console.error(err);
            showToast("Lỗi khi chuyển quyền chủ phòng", "danger");
        });
    };

    window.closeCurrentRoom = async function () {
        const isConfirmed = window.MoonDialog ? await window.MoonDialog.confirm({
            title: 'Đóng phòng xem chung',
            message: 'Bạn có chắc chắn muốn kết thúc và đóng phòng xem chung này?\nTất cả thành viên sẽ rời khỏi phòng.',
            confirmText: 'Đóng phòng',
            cancelText: 'Hủy',
            type: 'danger',
            iconClass: 'bi-power'
        }) : confirm("Bạn có chắc chắn muốn kết thúc và đóng phòng xem chung này?");

        if (!isConfirmed) return;

        showToast("Đang đóng phòng xem chung...", "info");
        connection.invoke("CloseRoom", config.roomCode).catch(err => {
            console.error(err);
            showToast("Lỗi khi đóng phòng: " + err, "danger");
        });
    };

    window.saveRoomSettings = function () {
        if (!roomState.isHost) return;
        const controlSelect = document.getElementById('wpControlSettingSelect');
        const danmakuSelect = document.getElementById('wpDanmakuSettingSelect');

        const onlyHost = controlSelect ? (controlSelect.value === 'true') : true;
        const allowDanmaku = danmakuSelect ? (danmakuSelect.value === 'true') : true;

        connection.invoke("UpdateSettings", config.roomCode, onlyHost, allowDanmaku)
            .then(() => {
                showToast("Đã lưu và đồng bộ cài đặt phòng thời gian thực!", "success");
            })
            .catch(err => {
                console.error(err);
                showToast("Lỗi khi cập nhật cài đặt", "danger");
            });
    };

    // Sidebar tab switching
    document.querySelectorAll('.wp-sidebar-tab').forEach(tab => {
        tab.addEventListener('click', () => {
            document.querySelectorAll('.wp-sidebar-tab').forEach(t => t.classList.remove('active'));
            tab.classList.add('active');

            const target = tab.dataset.target;
            document.querySelectorAll('.wp-tab-content').forEach(c => c.style.display = 'none');
            const targetEl = document.getElementById(target);
            if (targetEl) targetEl.style.display = 'flex';
        });
    });

    // Copy Share Link Button
    const btnCopyShare = document.getElementById('wpBtnCopyShare');
    if (btnCopyShare) {
        btnCopyShare.addEventListener('click', () => {
            const url = config.shareUrl || window.location.href;
            navigator.clipboard.writeText(url).then(() => {
                showToast("Đã sao chép link mời phòng xem chung vào bộ nhớ tạm!", "success");
                btnCopyShare.innerHTML = '<i class="fa-solid fa-check text-success"></i> Đã sao chép link';
                setTimeout(() => {
                    btnCopyShare.innerHTML = '<i class="fa-solid fa-share-nodes"></i> Chia sẻ phòng';
                }, 2500);
            });
        });
    }
});
