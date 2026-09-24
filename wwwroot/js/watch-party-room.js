/**
 * 🌙 MOONPHIM - WATCH PARTY ROOM CLIENT ENGINE
 * SignalR Realtime Synchronization, Danmaku & Chat
 */

document.addEventListener('DOMContentLoaded', () => {
    const config = window.WP_CONFIG || {};
    if (!config.roomCode) return;

    // Elements
    const video = document.getElementById('wpVideoPlayer');
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

    // State Flags
    let isSyncing = false;
    let currentHls = null;
    let danmaku = null;
    let graceTimer = null;
    let roomState = {
        isHost: config.isHost,
        hostUserId: config.hostUserId,
        onlyHostControl: config.onlyHostControl,
        isPlaying: false,
        currentTime: 0
    };

    // Initialize Danmaku Engine
    if (danmakuContainer) {
        danmaku = new DanmakuEngine(danmakuContainer);
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
        
        updateHostControlsUI();

        if (state.messages && state.messages.length > 0) {
            messagesContainer.innerHTML = '';
            state.messages.forEach(msg => appendMessage(msg));
        }

        if (state.members) {
            updateMemberList(state.members);
        }

        // Khởi tạo Video Player
        if (config.videoUrl) {
            loadVideo(config.videoUrl, state.currentTime, state.isPlaying);
        }
    });

    connection.on("OnSyncPlay", (data) => {
        if (!video) return;
        isSyncing = true;
        
        if (Math.abs(video.currentTime - data.currentTime) > 2) {
            video.currentTime = data.currentTime;
        }

        video.play().then(() => {
            setTimeout(() => { isSyncing = false; }, 300);
        }).catch(() => {
            isSyncing = false;
        });

        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tiếp tục phát`, '#60a5fa', 'top');
        }
    });

    connection.on("OnSyncPause", (data) => {
        if (!video) return;
        isSyncing = true;

        if (Math.abs(video.currentTime - data.currentTime) > 2) {
            video.currentTime = data.currentTime;
        }

        video.pause();
        setTimeout(() => { isSyncing = false; }, 300);

        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tạm dừng`, '#fca5a5', 'top');
        }
    });

    connection.on("OnSyncSeek", (data) => {
        if (!video) return;
        isSyncing = true;
        video.currentTime = data.currentTime;
        setTimeout(() => { isSyncing = false; }, 300);

        if (danmaku) {
            const minutes = Math.floor(data.currentTime / 60);
            const seconds = Math.floor(data.currentTime % 60);
            const timeStr = `${minutes}:${seconds < 10 ? '0' : ''}${seconds}`;
            danmaku.emit(`${data.senderName} đã tua đến ${timeStr}`, '#fbbf24', 'top');
        }
    });

    connection.on("OnEpisodeChanged", (data) => {
        appendSystemMessage(`Chủ phòng đã chuyển sang Tập ${data.episodeNumber} (${data.serverName})`);
        if (data.videoUrl) {
            loadVideo(data.videoUrl, 0, true);
        }
        
        // Highlight active episode button
        document.querySelectorAll('.wp-ep-item').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.episodeId == data.episodeId);
        });
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
        appendSystemMessage(`Cài đặt phòng: ${data.onlyHostControl ? "Chỉ chủ phòng điều khiển" : "Tất cả thành viên có thể điều khiển"}`);
    });

    connection.on("OnRoomClosed", (msg) => {
        alert(msg);
        window.location.href = "/watch-party";
    });

    connection.on("OnError", (err) => {
        alert(err);
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

    // ==========================================
    // 2️⃣ VIDEO PLAYER CONTROLS & SYNC
    // ==========================================
    function loadVideo(url, startTime = 0, autoPlay = false) {
        if (!video || !url) return;

        if (currentHls) {
            currentHls.destroy();
            currentHls = null;
        }

        if (url.includes('.m3u8') && window.Hls && Hls.isSupported()) {
            const hls = new Hls({ enableWorker: true });
            hls.loadSource(url);
            hls.attachMedia(video);
            hls.on(Hls.Events.MANIFEST_PARSED, () => {
                if (startTime > 0) video.currentTime = startTime;
                if (autoPlay) video.play().catch(() => {});
            });
            currentHls = hls;
        } else if (video.canPlayType('application/vnd.apple.mpegurl') || !url.includes('.m3u8')) {
            video.src = url;
            if (startTime > 0) video.currentTime = startTime;
            if (autoPlay) video.play().catch(() => {});
        }
    }

    // Video Event Listeners (Emit Sync)
    if (video) {
        video.addEventListener('play', () => {
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                // Không có quyền
                return;
            }
            connection.invoke("SyncPlay", config.roomCode, video.currentTime).catch(console.error);
        });

        video.addEventListener('pause', () => {
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                return;
            }
            connection.invoke("SyncPause", config.roomCode, video.currentTime).catch(console.error);
        });

        video.addEventListener('seeked', () => {
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                return;
            }
            connection.invoke("SyncSeek", config.roomCode, video.currentTime).catch(console.error);
        });
    }

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
        if (!danmakuInput) return;
        const text = danmakuInput.value.trim();
        if (!text) return;

        const color = danmakuColor ? danmakuColor.value : '#ffffff';
        const currentTime = video ? video.currentTime : 0;

        connection.invoke("SendDanmaku", config.roomCode, text, color, "scroll", currentTime).catch(console.error);
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

    const danmakuColorBadge = document.getElementById('wpDanmakuColorBadge');
    if (danmakuColor && danmakuColorBadge) {
        const updateColorUI = () => {
            const val = danmakuColor.value;
            danmakuColorBadge.style.color = val;
            danmakuColorBadge.style.boxShadow = `0 0 10px ${val}66`;
        };
        danmakuColor.addEventListener('input', updateColorUI);
        danmakuColor.addEventListener('change', updateColorUI);
        updateColorUI();
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
            if (danmakuInput) danmakuInput.focus();
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

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ==========================================
    // 4️⃣ HOST ACTIONS (GLOBAL WINDOW METHODS)
    // ==========================================
    window.changeEpisode = function (episodeId, episodeNumber, serverName, videoUrl) {
        if (!roomState.isHost) {
            alert("Chỉ chủ phòng mới có quyền đổi tập phim.");
            return;
        }
        connection.invoke("ChangeEpisode", config.roomCode, parseInt(episodeId), parseInt(episodeNumber), serverName, videoUrl)
            .catch(console.error);
    };

    window.transferHostTo = function (targetUserId) {
        if (!confirm("Bạn có chắc chắn muốn chuyển quyền chủ phòng cho thành viên này?")) return;
        connection.invoke("TransferHost", config.roomCode, parseInt(targetUserId)).catch(console.error);
    };

    window.closeCurrentRoom = function () {
        if (!confirm("Bạn có chắc chắn muốn kết thúc và đóng phòng xem chung này?")) return;
        connection.invoke("CloseRoom", config.roomCode).catch(console.error);
    };

    window.toggleControlSetting = function (onlyHost) {
        connection.invoke("UpdateSettings", config.roomCode, onlyHost).catch(console.error);
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
                btnCopyShare.innerHTML = '<i class="fa-solid fa-check text-success"></i> Đã sao chép link';
                setTimeout(() => {
                    btnCopyShare.innerHTML = '<i class="fa-solid fa-share-nodes"></i> Chia sẻ phòng';
                }, 2500);
            });
        });
    }
});
