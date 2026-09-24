/**
 * 🌙 MOONPHIM - WATCH PARTY HUB JAVASCRIPT
 * Handles Hub Tabs, Search Filter, Create Room Modal & Private PIN validation
 */

document.addEventListener('DOMContentLoaded', () => {
    // 1. Tab Switching (Public vs Private)
    const tabBtns = document.querySelectorAll('.wp-tab-btn');
    const tabPublic = document.getElementById('tab-public-rooms');
    const tabPrivate = document.getElementById('tab-private-rooms');

    tabBtns.forEach(btn => {
        btn.addEventListener('click', () => {
            tabBtns.forEach(b => b.classList.remove('active'));
            btn.classList.add('active');

            const type = btn.dataset.tab;
            if (type === 'public') {
                if (tabPublic) tabPublic.style.display = 'grid';
                if (tabPrivate) tabPrivate.style.display = 'none';
            } else {
                if (tabPublic) tabPublic.style.display = 'none';
                if (tabPrivate) tabPrivate.style.display = 'grid';
            }
        });
    });

    // 2. Search Filter for Room Cards
    const searchInput = document.getElementById('wpSearchInput');
    if (searchInput) {
        searchInput.addEventListener('input', (e) => {
            const query = e.target.value.toLowerCase().trim();
            const activeGrid = document.querySelector('.wp-grid[style*="display: grid"], .wp-grid:not([style*="display: none"])');
            if (!activeGrid) return;

            const cards = activeGrid.querySelectorAll('.wp-room-card');
            cards.forEach(card => {
                const text = card.textContent.toLowerCase();
                card.style.display = text.includes(query) ? '' : 'none';
            });
        });
    }

    // 3. Create Room Modal Logic
    const createModal = document.getElementById('wpCreateRoomModal');
    const btnOpenCreate = document.getElementById('wpBtnOpenCreate');
    const btnCloseCreate = document.getElementById('wpBtnCloseCreate');
    const movieSearchInput = document.getElementById('wpMovieSearch');
    const movieSearchResults = document.getElementById('wpMovieSearchResults');
    const selectedMovieIdInput = document.getElementById('wpSelectedMovieId');
    const selectedMovieInfo = document.getElementById('wpSelectedMovieInfo');
    const selectedPoster = document.getElementById('wpSelectedPoster');
    const selectedTitle = document.getElementById('wpSelectedTitle');
    const roomTitleInput = document.getElementById('wpRoomTitleInput');
    const serverSelect = document.getElementById('wpServerSelect');
    const episodeSelect = document.getElementById('wpEpisodeSelect');
    const isPrivateCheckbox = document.getElementById('wpIsPrivate');
    const pinGroup = document.getElementById('wpPinGroup');
    let currentMovieEpisodes = [];

    if (btnOpenCreate && createModal) {
        btnOpenCreate.addEventListener('click', () => {
            createModal.classList.add('show');
        });
    }

    if (btnCloseCreate && createModal) {
        btnCloseCreate.addEventListener('click', () => {
            createModal.classList.remove('show');
            if (movieSearchResults) movieSearchResults.classList.remove('show');
        });
    }

    // Close search results when clicking outside
    document.addEventListener('click', (e) => {
        if (movieSearchResults && !e.target.closest('.wp-search-wrapper')) {
            movieSearchResults.classList.remove('show');
        }
    });

    if (isPrivateCheckbox && pinGroup) {
        isPrivateCheckbox.addEventListener('change', () => {
            pinGroup.style.display = isPrivateCheckbox.checked ? 'block' : 'none';
        });
    }

    // Autocomplete Movie Search
    let searchDebounce = null;
    if (movieSearchInput && movieSearchResults) {
        movieSearchInput.addEventListener('input', (e) => {
            const val = e.target.value.trim();
            if (val.length < 2) {
                movieSearchResults.classList.remove('show');
                return;
            }

            clearTimeout(searchDebounce);
            searchDebounce = setTimeout(async () => {
                try {
                    const res = await fetch(`/watch-party/api/search-movies?q=${encodeURIComponent(val)}`);
                    const json = await res.json();

                    if (json.success && json.data && json.data.length > 0) {
                        movieSearchResults.innerHTML = '';
                        json.data.forEach(m => {
                            const item = document.createElement('div');
                            item.className = 'wp-search-item';
                            const poster = m.posterUrl || m.thumbUrl || '/images/default-poster.jpg';
                            item.innerHTML = `
                                <img src="${poster}" onerror="this.src='/images/default-poster.jpg';" />
                                <div style="min-width:0; flex-grow:1;">
                                    <div style="font-weight: 600; color: #fff; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${m.name}</div>
                                    <div style="font-size: 0.78rem; color: #94a3b8;">${m.originalName || ''} (${m.year || 'N/A'})</div>
                                </div>
                            `;

                            item.addEventListener('click', () => {
                                selectMovie(m);
                            });

                            movieSearchResults.appendChild(item);
                        });
                        movieSearchResults.classList.add('show');
                    } else {
                        movieSearchResults.innerHTML = '<div style="padding: 0.75rem; text-align: center; color: #94a3b8;">Không tìm thấy phim phù hợp</div>';
                        movieSearchResults.classList.add('show');
                    }
                } catch (err) {
                    console.error("Lỗi tìm kiếm phim:", err);
                }
            }, 250);
        });
    }

    function selectMovie(movie) {
        if (!movie) return;
        const movieId = movie.movieId || movie.MovieId;
        const movieName = movie.name || movie.Name || "";
        const poster = movie.posterUrl || movie.PosterUrl || movie.thumbUrl || movie.ThumbUrl || '/images/default-poster.jpg';

        if (selectedMovieIdInput) selectedMovieIdInput.value = movieId;
        if (selectedPoster) selectedPoster.src = poster;
        if (selectedTitle) selectedTitle.textContent = movieName;
        if (selectedMovieInfo) selectedMovieInfo.style.display = 'flex';
        if (movieSearchResults) movieSearchResults.classList.remove('show');
        if (movieSearchInput) movieSearchInput.value = movieName;

        // Auto generate default room title
        const hostName = window.WP_CURRENT_USER_NAME || "Bạn";
        if (roomTitleInput) {
            roomTitleInput.value = `Xem phim ${movieName} cùng ${hostName}`;
        }

        // Fetch servers & episodes
        loadEpisodesForMovie(movieId);
    }

    // When Server dropdown changes -> filter Episode dropdown
    if (serverSelect) {
        serverSelect.addEventListener('change', () => {
            const selectedServer = serverSelect.value;
            populateEpisodeDropdown(selectedServer);
        });
    }

    function populateEpisodeDropdown(serverName) {
        if (!episodeSelect) return;
        episodeSelect.innerHTML = '';

        const cleanServer = (serverName || '').trim().toLowerCase();
        const filtered = cleanServer 
            ? currentMovieEpisodes.filter(e => (e.serverName || 'Mặc định').trim().toLowerCase() === cleanServer)
            : currentMovieEpisodes;

        if (filtered.length > 0) {
            filtered.forEach((ep, idx) => {
                const opt = document.createElement('option');
                opt.value = ep.episodeId;
                opt.textContent = `Tập ${ep.episodeName}`;
                if (idx === 0) opt.selected = true;
                episodeSelect.appendChild(opt);
            });
            episodeSelect.value = filtered[0].episodeId;
        } else {
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = 'Tập 1 (Mặc định)';
            episodeSelect.appendChild(opt);
        }
    }

    async function loadEpisodesForMovie(movieId) {
        if (!serverSelect || !episodeSelect || !movieId) return;
        serverSelect.innerHTML = '<option value="">Đang tải Server...</option>';
        episodeSelect.innerHTML = '<option value="">Đang tải Tập...</option>';

        try {
            const res = await fetch(`/watch-party/api/movie-episodes?movieId=${movieId}`);
            const json = await res.json();
            
            if (json.success && json.data && json.data.length > 0) {
                currentMovieEpisodes = json.data;

                // Lấy danh sách server duy nhất
                const servers = [...new Set(currentMovieEpisodes.map(e => (e.serverName || 'Mặc định').trim()))];
                serverSelect.innerHTML = '';
                servers.forEach(s => {
                    const opt = document.createElement('option');
                    opt.value = s;
                    opt.textContent = s;
                    serverSelect.appendChild(opt);
                });

                // Ưu tiên chọn Server Vietsub nếu có, nếu không thì chọn Server đầu tiên
                let defaultServer = servers.find(s => s.toLowerCase().includes('vietsub')) || servers[0];
                serverSelect.value = defaultServer;
                populateEpisodeDropdown(defaultServer);
            } else {
                currentMovieEpisodes = [];
                serverSelect.innerHTML = '<option value="Mặc định">Server Mặc định</option>';
                episodeSelect.innerHTML = '<option value="">Tập 1 (Mặc định)</option>';
            }
        } catch (err) {
            console.error("Lỗi tải tập phim:", err);
            serverSelect.innerHTML = '<option value="Mặc định">Server Mặc định</option>';
            episodeSelect.innerHTML = '<option value="">Tập 1 (Mặc định)</option>';
        }
    }

    // 4. Private Room PIN Prompt Modal
    const pinModal = document.getElementById('wpPinPromptModal');
    const pinInput = document.getElementById('wpPinInput');
    const createPinInput = document.getElementById('wpCreatePinInput');
    const btnSubmitPin = document.getElementById('wpBtnSubmitPin');
    const btnCancelPin = document.getElementById('wpBtnCancelPin');
    let targetRoomCode = null;

    // Chỉ cho phép nhập số cho PIN (Tạo phòng & Vào phòng)
    if (createPinInput) {
        createPinInput.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/\D/g, '').slice(0, 6);
        });
    }

    if (pinInput) {
        pinInput.addEventListener('input', (e) => {
            e.target.value = e.target.value.replace(/\D/g, '').slice(0, 6);
        });
        pinInput.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                if (btnSubmitPin) btnSubmitPin.click();
            }
        });
    }

    // Form validation khi tạo phòng
    const createForm = document.querySelector('#wpCreateRoomModal form');
    if (createForm) {
        createForm.addEventListener('submit', (e) => {
            if (!selectedMovieIdInput || !selectedMovieIdInput.value) {
                e.preventDefault();
                if (window.MoonDialog) {
                    window.MoonDialog.alert({
                        title: 'Chưa chọn phim',
                        message: 'Vui lòng tìm và chọn một bộ phim bạn muốn xem cùng trước khi tạo phòng!',
                        type: 'warning',
                        iconClass: 'bi-film'
                    });
                }
                return;
            }

            if (isPrivateCheckbox && isPrivateCheckbox.checked) {
                const pin = createPinInput ? createPinInput.value.trim() : '';
                if (!/^\d{6}$/.test(pin)) {
                    e.preventDefault();
                    if (window.MoonDialog) {
                        window.MoonDialog.alert({
                            title: 'Mã PIN không hợp lệ',
                            message: 'Vui lòng nhập đúng 6 chữ số cho mã PIN phòng riêng tư (VD: 123456).',
                            type: 'warning',
                            iconClass: 'bi-key-fill'
                        });
                    }
                    if (createPinInput) createPinInput.focus();
                    return;
                }
            }
        });
    }

    window.promptPrivateRoom = function (roomCode) {
        targetRoomCode = roomCode;
        if (pinInput) pinInput.value = '';
        if (pinModal) pinModal.classList.add('show');
        setTimeout(() => { if (pinInput) pinInput.focus(); }, 150);
    };

    if (btnCancelPin && pinModal) {
        btnCancelPin.addEventListener('click', () => {
            pinModal.classList.remove('show');
            targetRoomCode = null;
        });
    }

    if (btnSubmitPin && pinModal) {
        btnSubmitPin.addEventListener('click', async () => {
            if (!targetRoomCode) return;
            const pin = pinInput ? pinInput.value.trim() : '';
            if (!pin) {
                if (window.MoonDialog) {
                    window.MoonDialog.alert({
                        title: 'Chưa nhập mã PIN',
                        message: 'Vui lòng nhập mã PIN gồm đúng 6 chữ số để tham gia phòng.',
                        type: 'warning',
                        iconClass: 'bi-key-fill'
                    });
                } else {
                    alert("Vui lòng nhập mã PIN phòng.");
                }
                return;
            }

            if (!/^\d{6}$/.test(pin)) {
                if (window.MoonDialog) {
                    window.MoonDialog.alert({
                        title: 'Mã PIN không đúng định dạng',
                        message: 'Mã PIN phòng phải bao gồm đúng 6 chữ số (VD: 123456).',
                        type: 'warning',
                        iconClass: 'bi-exclamation-triangle-fill'
                    });
                } else {
                    alert("Mã PIN phải gồm đúng 6 chữ số.");
                }
                return;
            }

            try {
                const res = await fetch('/watch-party/api/validate-pin', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ roomCode: targetRoomCode, pin: pin })
                });
                const json = await res.json();
                if (json.success && json.valid) {
                    window.location.href = `/watch-party/${targetRoomCode}`;
                } else {
                    if (window.MoonDialog) {
                        window.MoonDialog.alert({
                            title: 'Mã PIN không chính xác',
                            message: json.message || "Mã PIN không chính xác. Vui lòng thử lại!",
                            type: 'danger',
                            iconClass: 'bi-shield-x'
                        });
                    } else {
                        alert(json.message || "Mã PIN không chính xác!");
                    }
                }
            } catch (err) {
                console.error("Lỗi xác thực PIN:", err);
                if (window.MoonDialog) {
                    window.MoonDialog.alert({
                        title: 'Lỗi kết nối',
                        message: 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại đường truyền!',
                        type: 'danger',
                        iconClass: 'bi-wifi-off'
                    });
                } else {
                    alert("Không thể kết nối đến máy chủ.");
                }
            }
        });
    }

    // 5. SignalR Realtime Lobby Synchronization
    if (window.signalR) {
        const lobbyConnection = new signalR.HubConnectionBuilder()
            .withUrl("/watchPartyHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        function updateLobbyStats() {
            const publicCards = document.querySelectorAll('#tab-public-rooms .wp-room-card');
            const privateCards = document.querySelectorAll('#tab-private-rooms .wp-room-card');
            const totalRooms = publicCards.length + privateCards.length;

            const publicBadge = document.getElementById('wpPublicBadge');
            const privateBadge = document.getElementById('wpPrivateBadge');
            const totalActiveRoomsEl = document.getElementById('wpTotalActiveRooms');
            const totalWatchingUsersEl = document.getElementById('wpTotalWatchingUsers');

            if (publicBadge) publicBadge.textContent = publicCards.length;
            if (privateBadge) privateBadge.textContent = privateCards.length;
            if (totalActiveRoomsEl) totalActiveRoomsEl.textContent = totalRooms;

            let totalUsers = 0;
            document.querySelectorAll('.wp-card-member-count').forEach(el => {
                const text = el.textContent || '';
                const match = text.match(/^(\d+)/);
                if (match) {
                    totalUsers += parseInt(match[1], 10) || 0;
                }
            });
            if (totalWatchingUsersEl) totalWatchingUsersEl.textContent = totalUsers;
        }

        function createRoomCardElement(room) {
            const card = document.createElement('div');
            card.className = 'wp-room-card';
            card.id = `wp-card-${room.roomCode}`;
            card.dataset.roomCode = room.roomCode;
            card.style.opacity = '0';
            card.style.transform = 'translateY(15px)';
            card.style.transition = 'all 0.35s ease-out';

            const poster = room.posterUrl 
                ? (room.posterUrl.startsWith('http') ? room.posterUrl : `https://img.ophim.live/uploads/movies/${room.posterUrl.replace(/^\/+/, '')}`)
                : (room.thumbUrl 
                    ? (room.thumbUrl.startsWith('http') ? room.thumbUrl : `https://img.ophim.live/uploads/movies/${room.thumbUrl.replace(/^\/+/, '')}`)
                    : '/images/default-poster.jpg');

            const isPrivate = !!room.isPrivate;
            const badgeType = isPrivate 
                ? '<span class="wp-badge wp-badge-private"><i class="fa-solid fa-lock"></i> Cần PIN</span>'
                : '<span class="wp-badge wp-badge-public"><i class="fa-solid fa-globe"></i> Công khai</span>';

            const actionBtn = isPrivate
                ? `<button type="button" class="wp-btn-join" onclick="window.promptPrivateRoom('${room.roomCode}')">
                       <i class="fa-solid fa-key"></i> Nhập Mã PIN Để Vào
                   </button>`
                : `<a href="/watch-party/${room.roomCode}" class="wp-btn-join">
                       <i class="fa-solid fa-play"></i> Vào Phòng Xem Ngay
                   </a>`;

            card.innerHTML = `
                <div class="wp-room-poster" style="background-image: url('${poster}');">
                    <div class="wp-room-poster-overlay"></div>
                    <div class="wp-room-badges">
                        <span class="wp-badge wp-badge-live">
                            <i class="fa-solid fa-circle" style="font-size: 0.5rem;"></i> Trực tiếp
                        </span>
                        ${badgeType}
                    </div>
                    <span class="wp-room-code-tag">#${room.roomCode}</span>
                </div>

                <div class="wp-room-body">
                    <h3 class="wp-room-title" title="${escapeHtml(room.title)}">${escapeHtml(room.title)}</h3>
                    <div class="wp-room-movie-name">
                        <i class="fa-solid fa-film"></i>
                        <span>${escapeHtml(room.movieTitle)} (Tập ${room.episodeNumber || 1})</span>
                    </div>

                    <div class="wp-room-host-meta">
                        <div class="wp-host-info">
                            <img src="${room.hostAvatar || '/images/nouser.png'}" class="wp-host-avatar" onerror="this.src='/images/nouser.png';" />
                            <span class="wp-host-name">${escapeHtml(room.hostName)}</span>
                        </div>
                        <div class="wp-member-count">
                            <i class="fa-solid fa-users"></i>
                            <span class="wp-card-member-count" id="wp-member-count-${room.roomCode}">${room.currentMembersCount || 1} / ${room.maxMembers || 20}</span>
                        </div>
                    </div>

                    ${actionBtn}
                </div>
            `;
            return card;
        }

        function escapeHtml(str) {
            if (!str) return '';
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        }

        lobbyConnection.on("OnLobbyRoomCreated", (room) => {
            if (!room || !room.roomCode) return;
            if (document.getElementById(`wp-card-${room.roomCode}`)) return;

            const targetGrid = room.isPrivate ? tabPrivate : tabPublic;
            if (!targetGrid) return;

            // Xóa empty state nếu có
            const emptyEl = targetGrid.querySelector('.wp-empty-state');
            if (emptyEl) emptyEl.remove();

            const card = createRoomCardElement(room);
            targetGrid.prepend(card);

            requestAnimationFrame(() => {
                card.style.opacity = '1';
                card.style.transform = 'translateY(0)';
            });

            updateLobbyStats();
        });

        lobbyConnection.on("OnLobbyRoomClosed", (roomCode) => {
            if (!roomCode) return;
            const card = document.getElementById(`wp-card-${roomCode}`);
            if (card) {
                const parentGrid = card.parentElement;
                card.style.transition = 'all 0.35s ease-in';
                card.style.opacity = '0';
                card.style.transform = 'scale(0.9) translateY(-10px)';

                setTimeout(() => {
                    card.remove();
                    updateLobbyStats();

                    if (parentGrid && parentGrid.querySelectorAll('.wp-room-card').length === 0) {
                        const isPrivateGrid = (parentGrid.id === 'tab-private-rooms');
                        const emptyHtml = isPrivateGrid
                            ? `<div class="wp-empty-state" id="wpEmptyPrivate">
                                   <div class="wp-empty-icon"><i class="fa-solid fa-shield-halved"></i></div>
                                   <h3 class="wp-empty-title">Chưa có phòng riêng tư nào</h3>
                                   <p class="wp-empty-desc">Tạo phòng riêng tư với mã PIN để thưởng thức phim trọn vẹn cùng nhóm bạn thân thiết.</p>
                               </div>`
                            : `<div class="wp-empty-state" id="wpEmptyPublic">
                                   <div class="wp-empty-icon"><i class="fa-solid fa-tv"></i></div>
                                   <h3 class="wp-empty-title">Chưa có phòng công khai nào</h3>
                                   <p class="wp-empty-desc">Hãy là người đầu tiên tạo phòng xem chung và mời bạn bè cùng tham gia ngay!</p>
                                   <button type="button" class="wp-btn-create" onclick="document.getElementById('wpBtnOpenCreate')?.click()">
                                       <i class="fa-solid fa-plus"></i> Tạo Phòng Ngay
                                   </button>
                               </div>`;
                        parentGrid.innerHTML = emptyHtml;
                    }
                }, 350);
            }
        });

        lobbyConnection.on("OnLobbyRoomUpdated", (data) => {
            if (!data || !data.roomCode) return;
            const memberCountEl = document.getElementById(`wp-member-count-${data.roomCode}`);
            if (memberCountEl) {
                memberCountEl.textContent = `${data.currentMembersCount} / ${data.maxMembers}`;
            }
            updateLobbyStats();
        });

        async function startLobbySignalR() {
            try {
                await lobbyConnection.start();
                await lobbyConnection.invoke("JoinLobby");
            } catch (err) {
                console.warn("Lobby SignalR Connection Error (sẽ thử lại sau):", err);
                setTimeout(startLobbySignalR, 4000);
            }
        }

        startLobbySignalR();
    }
});
