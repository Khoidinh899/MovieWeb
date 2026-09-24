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

    // State Flags
    let isSyncing = false;
    let currentHls = null;
    let danmaku = null;
    let graceTimer = null;
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
    // 🔔 TOAST NOTIFICATION HELPER (Slide-up)
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
        
        updateHostControlsUI();
        updateDanmakuUI();

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
        removeAutoplayPrompt();
        
        if (Math.abs(video.currentTime - data.currentTime) > 1.5) {
            video.currentTime = data.currentTime;
        }

        const playPromise = video.play();
        if (playPromise !== undefined) {
            playPromise.then(() => {
                setTimeout(() => { isSyncing = false; }, 300);
            }).catch((err) => {
                console.warn("[WatchParty] Autoplay prevented by browser:", err);
                isSyncing = false;
                showAutoplayPrompt(data.currentTime);
            });
        } else {
            setTimeout(() => { isSyncing = false; }, 300);
        }

        if (danmaku) {
            danmaku.emit(`${data.senderName} đã tiếp tục phát`, '#60a5fa', 'top');
        }
    });

    connection.on("OnSyncPause", (data) => {
        if (!video) return;
        isSyncing = true;
        removeAutoplayPrompt();

        if (Math.abs(video.currentTime - data.currentTime) > 1.5) {
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

    connection.on("OnSyncHeartbeat", (data) => {
        if (!video || roomState.isHost) return;
        if (data.isPlaying && !video.paused) {
            if (Math.abs(video.currentTime - data.currentTime) > 3) {
                console.log(`[WatchParty] Aligning drift (${video.currentTime.toFixed(1)}s -> ${data.currentTime.toFixed(1)}s)`);
                isSyncing = true;
                video.currentTime = data.currentTime;
                setTimeout(() => { isSyncing = false; }, 300);
            }
        }
    });

    connection.on("OnEpisodeChanged", (data) => {
        appendSystemMessage(`Chủ phòng đã chuyển sang Tập ${data.episodeNumber} (${data.serverName})`);
        removeAutoplayPrompt();
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
            }, 3000);
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

    function isEmbedUrl(url) {
        if (!url) return false;
        const lower = url.toLowerCase();
        if (lower.includes('.m3u8')) return false;
        if (lower.includes('/video/') || lower.includes('/embed/') || lower.includes('streamvsmov') || lower.includes('vsmov') || lower.includes('player.phimapi.com') || lower.includes('youtube.com') || lower.includes('youtu.be')) return true;
        return !lower.includes('.m3u8');
    }

    // ==========================================
    // 2️⃣ VIDEO PLAYER CONTROLS & SYNC
    // ==========================================
    function loadVideo(url, startTime = 0, autoPlay = false) {
        if (!url || !url.trim()) return;
        url = url.trim();

        if (isEmbedUrl(url)) {
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
                let targetUrl = url;
                if (startTime > 0) {
                    if (targetUrl.includes('#t=')) {
                        targetUrl = targetUrl.replace(/#t=\d+/, `#t=${startTime}`);
                    } else if (targetUrl.includes('?')) {
                        targetUrl = `${targetUrl}&t=${startTime}#t=${startTime}`;
                    } else {
                        targetUrl = `${targetUrl}?t=${startTime}#t=${startTime}`;
                    }
                }
                if (embedPlayer.src !== targetUrl) {
                    embedPlayer.src = targetUrl;
                }
            }
            return;
        }

        // Direct Video / HLS M3U8 Mode
        if (embedPlayer) {
            embedPlayer.src = '';
            embedPlayer.style.display = 'none';
        }
        if (video) {
            video.style.display = 'block';
        }

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
            hls.on(Hls.Events.ERROR, (event, data) => {
                if (data.fatal) {
                    switch (data.type) {
                        case Hls.ErrorTypes.NETWORK_ERROR:
                            console.warn("HLS Network Error, attempting recovery...", data);
                            hls.startLoad();
                            break;
                        case Hls.ErrorTypes.MEDIA_ERROR:
                            console.warn("HLS Media Error, attempting recovery...", data);
                            hls.recoverMediaError();
                            break;
                        default:
                            hls.destroy();
                            break;
                    }
                }
            });
            currentHls = hls;
        } else if (video && (video.canPlayType('application/vnd.apple.mpegurl') || !url.includes('.m3u8'))) {
            video.src = url;
            if (startTime > 0) video.currentTime = startTime;
            if (autoPlay) video.play().catch(() => {});
        }
    }

    // Autoplay Prompt Helper
    function showAutoplayPrompt(targetTime) {
        let promptEl = document.getElementById('wpAutoplayPrompt');
        if (!promptEl) {
            promptEl = document.createElement('button');
            promptEl.id = 'wpAutoplayPrompt';
            promptEl.className = 'wp-autoplay-prompt';
            promptEl.innerHTML = '<i class="fa-solid fa-play"></i><span>Chủ phòng đang phát phim • Nhấp để đồng bộ ngay</span>';
            promptEl.onclick = () => {
                if (video) {
                    if (targetTime !== undefined && targetTime > 0) video.currentTime = targetTime;
                    video.play().catch(console.error);
                }
                removeAutoplayPrompt();
            };
            const container = document.getElementById('wpVideoContainer');
            if (container) container.appendChild(promptEl);
        }
    }

    function removeAutoplayPrompt() {
        const promptEl = document.getElementById('wpAutoplayPrompt');
        if (promptEl) promptEl.remove();
    }

    // Host Periodic Playback Heartbeat (every 5 seconds while playing)
    setInterval(() => {
        if (roomState.isHost && video && !video.paused && connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke("SyncHeartbeat", config.roomCode, video.currentTime).catch(() => {});
        }
    }, 5000);

    // Eagerly initialize video on DOM Load so player does not appear blank
    if (config.videoUrl) {
        loadVideo(config.videoUrl, 0, false);
    }

    // Video Event Listeners (Emit Sync)
    if (video) {
        video.addEventListener('play', () => {
            removeAutoplayPrompt();
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                showToast("Chỉ chủ phòng mới có quyền điều khiển phát video.", "warning");
                return;
            }
            connection.invoke("SyncPlay", config.roomCode, video.currentTime).catch(console.error);
        });

        video.addEventListener('pause', () => {
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                showToast("Chỉ chủ phòng mới có quyền tạm dừng video.", "warning");
                return;
            }
            connection.invoke("SyncPause", config.roomCode, video.currentTime).catch(console.error);
        });

        video.addEventListener('seeked', () => {
            if (isSyncing) return;
            if (roomState.onlyHostControl && !roomState.isHost) {
                showToast("Chỉ chủ phòng mới có quyền tua video.", "warning");
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
        if (!roomState.allowDanmaku) {
            showToast("Chủ phòng đã tắt tính năng bình luận bay trong phòng này.", "warning");
            return;
        }
        if (!danmakuInput) return;
        const text = danmakuInput.value.trim();
        if (!text) return;

        const color = danmakuColor ? danmakuColor.value : '#ffffff';
        const currentTime = video ? video.currentTime : 0;

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
