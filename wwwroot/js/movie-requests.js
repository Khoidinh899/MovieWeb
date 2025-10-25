document.addEventListener('DOMContentLoaded', function () {

    // Lấy CSRF token từ hidden input
    const csrfToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    // --- Xử lý các nút thay đổi Status ---

    document.querySelectorAll('.btn-process').forEach(button => {
        button.addEventListener('click', function () {
            const requestId = this.getAttribute('data-id');
            if (confirm(`Bạn có chắc muốn nhận xử lý yêu cầu ID: ${requestId}?`)) {
                callSetStatusApi(requestId, 'Đang xử lý'); // ✅ SỬA: tiếng Việt
            }
        });
    });

    document.querySelectorAll('.btn-return-pending').forEach(button => {
        button.addEventListener('click', function () {
            const requestId = this.getAttribute('data-id');
            if (confirm(`Bạn có chắc muốn trả lại yêu cầu ID: ${requestId} về trạng thái Chờ xử lý?`)) {
                callSetStatusApi(requestId, 'Chờ đồng bộ', 'Admin trả lại chờ xử lý');
            }
        });
    });

    // =========================================================
    // NÚT MỚI 1: TỰ ĐỘNG (AUTO SYNC)
    // =========================================================
    document.querySelectorAll('.btn-auto-sync').forEach(button => {
        button.addEventListener('click', function () {
            const requestId = this.getAttribute('data-id');
            const movieTitle = this.getAttribute('data-title');
            const movieYear = this.getAttribute('data-year');

            if (!confirm(`🤖 TỰ ĐỘNG tìm và đồng bộ phim:\n"${movieTitle}" (${movieYear})\n\nBạn có chắc chắn?`)) {
                return;
            }

            // Hiện loading
            const btnElement = this;
            const originalHTML = btnElement.innerHTML;
            btnElement.disabled = true;
            btnElement.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang xử lý...';

            console.log('Auto sync request:', { requestId, movieTitle, movieYear });

            fetch('/Admin/AutoSyncMovieRequest', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                },
                body: new URLSearchParams({
                    '__RequestVerificationToken': csrfToken,
                    'requestId': requestId
                })
            })
                .then(response => response.json())
                .then(data => {
                    console.log('Auto sync response:', data);

                    if (data.success) {
                        // Trường hợp 1: Cần admin chọn phim (nhiều kết quả)
                        if (data.needsSelection && data.options) {
                            showMovieSelectionModal(requestId, data.options);
                            btnElement.disabled = false;
                            btnElement.innerHTML = originalHTML;
                        }
                        // Trường hợp 2: Đã sync thành công
                        else {
                            alert(`✅ ${data.message}\n\nPhim: ${data.movieName || 'N/A'}\nID: ${data.movieId || 'N/A'}\n\nTrang sẽ tải lại...`);
                            window.location.reload();
                        }
                    } else {
                        alert('❌ Lỗi: ' + data.message);
                        btnElement.disabled = false;
                        btnElement.innerHTML = originalHTML;
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert('❌ Có lỗi xảy ra: ' + error.message);
                    btnElement.disabled = false;
                    btnElement.innerHTML = originalHTML;
                });
        });
    });

    // =========================================================
    // NÚT MỚI 2: XÁC NHẬN (CONFIRM REQUEST)
    // =========================================================
    document.querySelectorAll('.btn-confirm-request').forEach(button => {
        button.addEventListener('click', function () {
            const requestId = this.getAttribute('data-id');
            const movieName = this.getAttribute('data-movie-name');

            if (!confirm(`✅ Xác nhận phim đúng:\n"${movieName}"\n\nSau khi xác nhận, bạn có thể gửi thông báo cho users.`)) {
                return;
            }

            console.log('Confirm request:', requestId);

            fetch('/Admin/ConfirmMovieRequest', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                },
                body: new URLSearchParams({
                    '__RequestVerificationToken': csrfToken,
                    'requestId': requestId
                })
            })
                .then(response => response.json())
                .then(data => {
                    console.log('Confirm response:', data);
                    if (data.success) {
                        alert('✅ ' + data.message);
                        window.location.reload();
                    } else {
                        alert('❌ Lỗi: ' + data.message);
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert('❌ Có lỗi xảy ra: ' + error.message);
                });
        });
    });

    // =========================================================
    // NÚT MỚI 3: GỬI THÔNG BÁO (SEND NOTIFICATION)
    // =========================================================
    document.querySelectorAll('.btn-send-notification').forEach(button => {
        button.addEventListener('click', function () {
            const requestId = this.getAttribute('data-id');
            const movieName = this.getAttribute('data-movie-name');

            if (!confirm(`📧 Gửi thông báo cho tất cả users đã yêu cầu phim:\n"${movieName}"\n\nBạn có chắc chắn?`)) {
                return;
            }

            // Hiện loading
            const btnElement = this;
            const originalHTML = btnElement.innerHTML;
            btnElement.disabled = true;
            btnElement.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang gửi...';

            console.log('Send notification:', requestId);

            fetch('/Admin/SendMovieNotification', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                },
                body: new URLSearchParams({
                    '__RequestVerificationToken': csrfToken,
                    'requestId': requestId
                })
            })
                .then(response => response.json())
                .then(data => {
                    console.log('Send notification response:', data);
                    if (data.success) {
                        alert('✅ ' + data.message);
                        btnElement.disabled = false;
                        btnElement.innerHTML = originalHTML;
                        // Không reload vì có thể gửi lại nhiều lần
                    } else {
                        alert('❌ Lỗi: ' + data.message);
                        btnElement.disabled = false;
                        btnElement.innerHTML = originalHTML;
                    }
                })
                .catch(error => {
                    console.error('Error:', error);
                    alert('❌ Có lỗi xảy ra: ' + error.message);
                    btnElement.disabled = false;
                    btnElement.innerHTML = originalHTML;
                });
        });
    });

    // =========================================================
    // HÀM HIỂN thị MODAL CHỌN PHIM (KHI CÓ NHIỀU KẾT QUẢ)
    // =========================================================
    function showMovieSelectionModal(requestId, options) {
        // Tạo HTML cho modal
        let modalHTML = `
        <div class="modal fade" id="movieSelectionModal" tabindex="-1" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header bg-warning">
                        <h5 class="modal-title">
                            <i class="fas fa-exclamation-triangle me-2"></i>Chọn phim chính xác
                        </h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        <p class="alert alert-info">
                            <i class="fas fa-info-circle me-2"></i>
                            Tìm thấy <strong>${options.length}</strong> kết quả. Vui lòng chọn phim chính xác:
                        </p>
                        <div class="list-group" id="movieSelectionList">
        `;

        options.forEach(movie => {
            const posterUrl = movie.posterUrl || '/images/default-poster.jpg';
            modalHTML += `
                <button type="button" class="list-group-item list-group-item-action d-flex align-items-center movie-option"
                        data-slug="${movie.slug}"
                        data-name="${movie.name}"
                        data-year="${movie.year}">
                    <img src="https://img.ophim.live/uploads/movies/${posterUrl}" 
                         alt="${movie.name}" 
                         width="50" 
                         class="me-3"
                         onerror="this.src='/images/default-poster.jpg'">
                    <div>
                        <h6 class="mb-0">${movie.name}</h6>
                        <small class="text-muted">${movie.originalName || ''} (${movie.year}) - ${movie.type || ''}</small>
                    </div>
                </button>
            `;
        });

        modalHTML += `
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Hủy</button>
                    </div>
                </div>
            </div>
        </div>
        `;

        // Xóa modal cũ (nếu có)
        const oldModal = document.getElementById('movieSelectionModal');
        if (oldModal) oldModal.remove();

        // Thêm modal mới vào body
        document.body.insertAdjacentHTML('beforeend', modalHTML);

        // Hiển thị modal
        const modal = new bootstrap.Modal(document.getElementById('movieSelectionModal'));
        modal.show();

        // Gán sự kiện click cho các option
        document.querySelectorAll('.movie-option').forEach(btn => {
            btn.addEventListener('click', function () {
                const slug = this.getAttribute('data-slug');
                const name = this.getAttribute('data-name');
                const year = this.getAttribute('data-year');

                if (confirm(`Xác nhận chọn phim:\n"${name}" (${year})\n\nSlug: ${slug}`)) {
                    modal.hide();
                    confirmAutoSyncWithSlug(requestId, slug);
                }
            });
        });
    }

    // =========================================================
    // HÀM GỌI API XÁC NHẬN SYNC VỚI SLUG ĐÃ CHỌN
    // =========================================================
    function confirmAutoSyncWithSlug(requestId, slug) {
        console.log('Confirming auto sync with slug:', { requestId, slug });

        // Hiện loading toàn trang
        const loadingDiv = document.createElement('div');
        loadingDiv.className = 'position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center';
        loadingDiv.style.backgroundColor = 'rgba(0,0,0,0.7)';
        loadingDiv.style.zIndex = '9999';
        loadingDiv.innerHTML = `
            <div class="text-center text-white">
                <div class="spinner-border mb-3" style="width: 3rem; height: 3rem;"></div>
                <h4>Đang đồng bộ phim...</h4>
                <p>Vui lòng chờ trong giây lát</p>
            </div>
        `;
        document.body.appendChild(loadingDiv);

        fetch('/Admin/ConfirmAutoSync', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: new URLSearchParams({
                '__RequestVerificationToken': csrfToken,
                'requestId': requestId,
                'slug': slug
            })
        })
            .then(response => response.json())
            .then(data => {
                loadingDiv.remove();
                console.log('Confirm auto sync response:', data);

                if (data.success) {
                    alert(`✅ ${data.message}\n\nPhim: ${data.movieName || 'N/A'}\nID: ${data.movieId || 'N/A'}\n\nTrang sẽ tải lại...`);
                    window.location.reload();
                } else {
                    alert('❌ Lỗi: ' + data.message);
                }
            })
            .catch(error => {
                loadingDiv.remove();
                console.error('Error:', error);
                alert('❌ Có lỗi xảy ra: ' + error.message);
            });
    }
    // =========================================================
    // XỬ LÝ MODAL XEM THÔNG TIN PHIM ĐÃ SYNC
    // =========================================================
    const viewSyncedMovieModal = document.getElementById('viewSyncedMovieModal');
    if (viewSyncedMovieModal) {
        viewSyncedMovieModal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;

            // Lấy data từ button
            const movieId = button.getAttribute('data-movie-id');
            const movieName = button.getAttribute('data-movie-name');
            const movieYear = button.getAttribute('data-movie-year');
            const movieSlug = button.getAttribute('data-movie-slug');
            const moviePoster = button.getAttribute('data-movie-poster');
            const movieType = button.getAttribute('data-movie-type');
            const adminNote = button.getAttribute('data-admin-note');

            // Điền thông tin vào modal
            document.getElementById('syncedMovieId').textContent = movieId || 'N/A';
            document.getElementById('syncedMovieName').textContent = movieName || 'N/A';
            document.getElementById('syncedMovieYear').textContent = movieYear || 'N/A';
            document.getElementById('syncedMovieType').textContent = movieType || 'N/A';
            document.getElementById('syncedMovieSlug').textContent = movieSlug || 'N/A';
            document.getElementById('syncedMovieAdminNote').textContent = adminNote || 'Không có ghi chú';

            // Link xem phim
            const movieLink = `/phim/${movieSlug}`;
            document.getElementById('syncedMovieLink').href = movieLink;

            // Poster
            const posterUrl = moviePoster
                ? `https://img.ophim.live/uploads/movies/${moviePoster}`
                : '/images/default-poster.jpg';
            document.getElementById('syncedMoviePoster').src = posterUrl;

            console.log('Viewing synced movie:', { movieId, movieName, movieSlug });
        });
    }

    // Hàm gọi API SetRequestStatus (AJAX) - ✅ SỬA: Token vào body
    function callSetStatusApi(requestId, status, note = '') {
        if (!csrfToken) {
            alert('Lỗi: Không tìm thấy CSRF token.');
            console.error('CSRF Token not found!');
            return;
        }

        console.log('Calling SetRequestStatus:', { requestId, status, note }); // Debug log

        fetch('/Admin/SetRequestStatus', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: new URLSearchParams({
                '__RequestVerificationToken': csrfToken, // ✅ SỬA: Gửi qua body
                'id': requestId,
                'status': status,
                'adminNote': note
            })
        })
            .then(response => {
                console.log('Response status:', response.status); // Debug log
                return response.json();
            })
            .then(data => {
                console.log('Response data:', data); // Debug log
                if (data.success) {
                    alert(data.message);
                    window.location.reload();
                } else {
                    alert('Lỗi: ' + data.message);
                }
            })
            .catch(error => {
                console.error('Error:', error);
                alert('Có lỗi xảy ra khi gọi API: ' + error.message);
            });
    }


    // --- Xử lý Modal Link Phim ---
    const linkMovieModal = document.getElementById('linkMovieModal');
    if (linkMovieModal) {
        const modalRequestIdInput = document.getElementById('modalRequestId');
        const modalRequestTitleSpan = document.getElementById('modalRequestTitle');
        const movieSearchInput = document.getElementById('movieSearchInput');
        const movieSearchResultsDiv = document.getElementById('movieSearchResults');
        const selectedMovieInfoSpan = document.getElementById('selectedMovieInfo');
        const selectedMovieIdInput = document.getElementById('selectedMovieId');
        const confirmLinkMovieButton = document.getElementById('confirmLinkMovieButton');

        linkMovieModal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;
            const requestId = button.getAttribute('data-request-id');
            const movieTitle = button.getAttribute('data-movie-title');

            console.log('Modal opened for request:', requestId, movieTitle); // Debug log

            modalRequestIdInput.value = requestId;
            modalRequestTitleSpan.textContent = movieTitle + ` (ID: ${requestId})`;
            movieSearchInput.value = movieTitle;
            movieSearchResultsDiv.innerHTML = '<div class="text-center text-muted">Bắt đầu tìm kiếm...</div>';
            selectedMovieInfoSpan.textContent = 'Chưa chọn phim nào';
            selectedMovieIdInput.value = '';
            confirmLinkMovieButton.disabled = true;

            searchMovies();
        });

        let searchTimeout;
        movieSearchInput.addEventListener('input', function () {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(searchMovies, 500);
        });

        // Hàm tìm kiếm phim (AJAX)
        function searchMovies() {
            const searchTerm = movieSearchInput.value.trim();
            movieSearchResultsDiv.innerHTML = '<div class="text-center"><div class="spinner-border spinner-border-sm" role="status"><span class="visually-hidden">Loading...</span></div> Đang tìm...</div>';

            if (searchTerm.length < 2) {
                movieSearchResultsDiv.innerHTML = '<div class="text-center text-muted">Nhập ít nhất 2 ký tự</div>';
                return;
            }

            console.log('Searching movies with term:', searchTerm); // Debug log

            fetch(`/api/movies/search?term=${encodeURIComponent(searchTerm)}`)
                .then(response => {
                    console.log('Search response status:', response.status); // Debug log
                    return response.json();
                })
                .then(data => {
                    console.log('Search response data:', data);

                    if (data.success && data.data && data.data.length > 0) {

                        // 1. Định nghĩa hằng số ở ngoài vòng lặp (để tối ưu)
                        const imageBasePath = 'https://img.ophim.live/uploads/movies/';
                        // const defaultImage = '/images/default-poster.jpg';

                        let resultsHtml = '<ul class="list-group">';

                        // 2. Lặp qua từng phim
                        data.data.forEach(movie => {
                            let posterFilename = movie.posterUrl; // Tên file từ DB
                            let finalImageUrl = defaultImage; // Mặc định là ảnh dự phòng

                            // 3. Kiểm tra file name có tồn tại không
                            if (posterFilename && posterFilename !== 'null' && posterFilename.trim() !== '') {
                                // 4. Luôn luôn ghép base path với tên file
                                finalImageUrl = imageBasePath + posterFilename;
                            }

                            // 5. Tạo HTML cho 1 dòng kết quả
                            resultsHtml += `
                            <li class="list-group-item list-group-item-action d-flex align-items-center movie-search-result"
                                style="cursor: pointer;"
                                data-movie-id="${movie.movieId}"
                                data-movie-name="${movie.name}"
                                data-movie-year="${movie.year}">
                                
                                <img src="${finalImageUrl}" alt="${movie.name}" width="30" class="me-2">
                                
                                ${movie.name} (${movie.year})
                            </li>`;
                        });

                        resultsHtml += '</ul>';
                        movieSearchResultsDiv.innerHTML = resultsHtml;

                        // 6. Gán sự kiện click cho các kết quả mới
                        addSearchResultClickListeners();

                    } else {
                        // Xử lý khi API trả về thành công nhưng không có phim nào
                        movieSearchResultsDiv.innerHTML = '<div class="text-center text-muted">Không tìm thấy phim nào.</div>';
                    }
                })
                .catch(error => {
                    // Xử lý khi fetch() bị lỗi (network, API sập, v.v.)
                    console.error('Error searching movies:', error);
                    movieSearchResultsDiv.innerHTML = '<div class="text-center text-danger">Lỗi khi tìm kiếm phim: ' + error.message + '</div>';
                });
        }

        // Hàm để gán sự kiện click cho các kết quả tìm kiếm
        function addSearchResultClickListeners() {
            document.querySelectorAll('.movie-search-result').forEach(item => {
                item.addEventListener('click', function () {
                    const movieId = this.getAttribute('data-movie-id');
                    const movieName = this.getAttribute('data-movie-name');
                    const movieYear = this.getAttribute('data-movie-year');
                    console.log('Movie selected:', movieId, movieName, movieYear); // Debug log
                    selectMovie(movieId, movieName, movieYear, this);
                });
            });
        }


        // Hàm được gọi khi user click chọn 1 phim từ kết quả
        function selectMovie(movieId, movieName, movieYear, listItemElement) {
            selectedMovieInfoSpan.textContent = `${movieName} (${movieYear}) - ID: ${movieId}`;
            selectedMovieIdInput.value = movieId;
            confirmLinkMovieButton.disabled = false;

            // Highlight dòng đã chọn
            document.querySelectorAll('#movieSearchResults li').forEach(li => li.classList.remove('active'));
            if (listItemElement) {
                listItemElement.classList.add('active');
            }
        }

        // Xử lý nút Xác nhận Liên kết - ✅ SỬA: Token vào body
        confirmLinkMovieButton.addEventListener('click', function () {
            const requestId = modalRequestIdInput.value;
            const movieId = selectedMovieIdInput.value;

            if (!movieId) {
                alert('Vui lòng chọn một phim để liên kết.');
                return;
            }

            if (confirm(`Bạn có chắc muốn liên kết yêu cầu ID: ${requestId} với phim ID: ${movieId}?`)) {
                if (!csrfToken) {
                    alert('Lỗi: Không tìm thấy CSRF token.');
                    return;
                }

                console.log('Linking movie:', { requestId, movieId }); // Debug log

                fetch('/Admin/LinkMovieToRequest', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded'
                    },
                    body: new URLSearchParams({
                        '__RequestVerificationToken': csrfToken, // ✅ SỬA: Gửi qua body
                        'requestId': requestId,
                        'movieId': movieId
                    })
                })
                    .then(response => {
                        console.log('Link response status:', response.status); // Debug log
                        return response.json();
                    })
                    .then(data => {
                        console.log('Link response data:', data); // Debug log
                        if (data.success) {
                            alert(data.message);
                            const modalInstance = bootstrap.Modal.getInstance(linkMovieModal);
                            if (modalInstance) {
                                modalInstance.hide();
                            }
                            window.location.reload();
                        } else {
                            alert('Lỗi: ' + data.message);
                        }
                    })
                    .catch(error => {
                        console.error('Error:', error);
                        alert('Có lỗi xảy ra khi gọi API: ' + error.message);
                    });
            }
        });
    }

});