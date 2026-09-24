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
        if (selectedMovieIdInput) selectedMovieIdInput.value = movie.movieId;
        if (selectedPoster) selectedPoster.src = movie.posterUrl || movie.thumbUrl || '/images/default-poster.jpg';
        if (selectedTitle) selectedTitle.textContent = movie.name;
        if (selectedMovieInfo) selectedMovieInfo.style.display = 'flex';
        if (movieSearchResults) movieSearchResults.classList.remove('show');
        if (movieSearchInput) movieSearchInput.value = movie.name;

        // Auto generate default room title
        const hostName = window.WP_CURRENT_USER_NAME || "Bạn";
        if (roomTitleInput) {
            roomTitleInput.value = `Xem phim ${movie.name} cùng ${hostName}`;
        }

        // Fetch servers & episodes
        loadEpisodesForMovie(movie.movieId);
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

        const filtered = serverName 
            ? currentMovieEpisodes.filter(e => (e.serverName || 'Mặc định') === serverName)
            : currentMovieEpisodes;

        if (filtered.length > 0) {
            filtered.forEach(ep => {
                const opt = document.createElement('option');
                opt.value = ep.episodeId;
                opt.textContent = `Tập ${ep.episodeName}`;
                episodeSelect.appendChild(opt);
            });
        } else {
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = 'Tập 1 (Mặc định)';
            episodeSelect.appendChild(opt);
        }
    }

    async function loadEpisodesForMovie(movieId) {
        if (!serverSelect || !episodeSelect) return;
        serverSelect.innerHTML = '<option value="">Đang tải...</option>';
        episodeSelect.innerHTML = '<option value="">Đang tải...</option>';

        try {
            const res = await fetch(`/watch-party/api/movie-episodes?movieId=${movieId}`);
            const json = await res.json();
            
            if (json.success && json.data && json.data.length > 0) {
                currentMovieEpisodes = json.data;

                // Lấy danh sách server duy nhất
                const servers = [...new Set(currentMovieEpisodes.map(e => e.serverName || 'Mặc định'))];
                serverSelect.innerHTML = '';
                servers.forEach(s => {
                    const opt = document.createElement('option');
                    opt.value = s;
                    opt.textContent = s;
                    serverSelect.appendChild(opt);
                });

                // Chọn server đầu tiên và hiển thị tập tương ứng
                if (servers.length > 0) {
                    serverSelect.value = servers[0];
                    populateEpisodeDropdown(servers[0]);
                }
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
    const btnSubmitPin = document.getElementById('wpBtnSubmitPin');
    const btnCancelPin = document.getElementById('wpBtnCancelPin');
    let targetRoomCode = null;

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
                alert("Vui lòng nhập mã PIN phòng.");
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
                    alert(json.message || "Mã PIN không chính xác!");
                }
            } catch (err) {
                console.error("Lỗi xác thực PIN:", err);
                alert("Không thể kết nối đến máy chủ.");
            }
        });
    }
});
