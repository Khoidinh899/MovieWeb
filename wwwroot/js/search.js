document.addEventListener('DOMContentLoaded', function () {
    
    // ========== DESKTOP SEARCH ==========
    const desktopInput = document.querySelector('.search-box input[name="keyword"]');
    
    if (desktopInput) {
        const wrapper = desktopInput.closest('.search-input-wrapper');
        if (!wrapper) return;
        
        wrapper.style.position = wrapper.style.position || 'relative';
        
        // Tạo suggestion box
        const suggestionBox = document.createElement('div');
        suggestionBox.className = 'search-suggestions shadow';
        wrapper.appendChild(suggestionBox);

        // Tạo overlay
        let overlay = document.querySelector('.search-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'search-overlay';
            document.body.appendChild(overlay);
        }

        let desktopTimer;
        
        // Xử lý khi gõ input desktop
        desktopInput.addEventListener('input', function () {
            clearTimeout(desktopTimer);
            const q = this.value.trim();

            if (q.length < 2) {
                suggestionBox.style.display = 'none';
                overlay.classList.remove('active');
                return;
            }

            desktopTimer = setTimeout(async () => {
                try {
                    const res = await fetch(`/api/goi-y-tim-kiem?keyword=${encodeURIComponent(q)}`);
                    const data = await res.json();

                    if (!data || data.length === 0) {
                        suggestionBox.innerHTML = '<div class="suggestion-empty">Không có kết quả</div>';
                        suggestionBox.style.display = 'block';
                        overlay.classList.add('active');
                        return;
                    }

                    suggestionBox.innerHTML = data.map(item => {
                        const name = (item.name || '').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                        const slug = encodeURIComponent(item.slug || '');
                        const img = item.image || '/images/no-image.jpg';
                        return `
                            <a href="/phim/${slug}" class="suggestion-item text-decoration-none">
                                <img src="${img}" onerror="this.src='/images/no-image.jpg'" alt="${name}">
                                <div class="text-truncate">${name}</div>
                            </a>
                        `;
                    }).join('');

                    suggestionBox.style.display = 'block';
                    overlay.classList.add('active');
                    suggestionBox.scrollTop = 0;
                    
                } catch (err) {
                    console.error('Desktop search error:', err);
                    suggestionBox.style.display = 'none';
                    overlay.classList.remove('active');
                }
            }, 220);
        });

        // Ẩn khi click ra ngoài
        document.addEventListener('click', (e) => {
            if (!wrapper.contains(e.target) && !overlay.contains(e.target)) {
                suggestionBox.style.display = 'none';
                overlay.classList.remove('active');
            }
        });

        // Ẩn khi click vào overlay
        overlay.addEventListener('click', () => {
            suggestionBox.style.display = 'none';
            overlay.classList.remove('active');
        });
        
        // Ngăn scroll trang khi scroll trong suggestion box
        suggestionBox.addEventListener('wheel', (e) => {
            const isScrollable = suggestionBox.scrollHeight > suggestionBox.clientHeight;
            if (isScrollable) {
                e.stopPropagation();
                
                const isAtTop = suggestionBox.scrollTop === 0;
                const isAtBottom = suggestionBox.scrollTop + suggestionBox.clientHeight >= suggestionBox.scrollHeight;
                
                if ((isAtTop && e.deltaY < 0) || (isAtBottom && e.deltaY > 0)) {
                    e.preventDefault();
                }
            }
        }, { passive: false });
    }
    
    // ========== MOBILE SEARCH (ROBUST & PRODUCTION READY) ==========
    const mobileSearchBtn = document.getElementById('mobileSearchBtn');
    const mobileSearchOverlay = document.getElementById('mobileSearchOverlay');
    const mobileSearchClose = document.getElementById('mobileSearchClose');
    const mobileSearchInput = document.getElementById('mobileSearchInput');
    const mobileSearchResults = document.getElementById('mobileSearchResults');
    
    if (mobileSearchOverlay) {
        const openMobileSearch = () => {
            mobileSearchOverlay.classList.add('active');
            document.body.classList.add('mobile-search-active');
            
            setTimeout(() => {
                if (mobileSearchInput) {
                    mobileSearchInput.focus();
                }
            }, 100);
        };

        const closeMobileSearch = () => {
            mobileSearchOverlay.classList.remove('active');
            document.body.classList.remove('mobile-search-active');
            if (mobileSearchInput) {
                mobileSearchInput.value = '';
            }
            if (mobileSearchResults) {
                mobileSearchResults.innerHTML = '';
            }
        };

        if (mobileSearchBtn) {
            mobileSearchBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                openMobileSearch();
            });
        }
        
        if (mobileSearchClose) {
            mobileSearchClose.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                closeMobileSearch();
            });
        }
        
        // Đóng khi ấn Escape
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && mobileSearchOverlay.classList.contains('active')) {
                closeMobileSearch();
            }
        });
        
        // Xử lý tìm kiếm mobile
        let mobileTimer;
        if (mobileSearchInput && mobileSearchResults) {
            mobileSearchInput.addEventListener('input', function () {
                clearTimeout(mobileTimer);
                const q = this.value.trim();

                if (q.length < 2) {
                    mobileSearchResults.innerHTML = '';
                    return;
                }

                mobileTimer = setTimeout(async () => {
                    try {
                        const res = await fetch(`/api/goi-y-tim-kiem?keyword=${encodeURIComponent(q)}`);
                        if (!res.ok) throw new Error('Network response not ok');
                        const data = await res.json();

                        if (!data || data.length === 0) {
                            mobileSearchResults.innerHTML = `
                                <div class="mobile-search-empty">
                                    <i class="fas fa-search"></i>
                                    <p>Không tìm thấy kết quả phù hợp</p>
                                </div>
                            `;
                            return;
                        }

                        mobileSearchResults.innerHTML = data.map(item => {
                            const name = (item.name || '').replace(/</g, '&lt;').replace(/>/g, '&gt;');
                            const slug = encodeURIComponent(item.slug || '');
                            const img = item.image || '/images/default-poster.jpg';
                            const year = item.year || '';
                            
                            return `
                                <a href="/phim/${slug}" class="mobile-suggestion-item">
                                    <img src="${img}" onerror="this.onerror=null;this.src='/images/default-poster.jpg'" alt="${name}">
                                    <div class="movie-info">
                                        <div class="movie-title">${name}</div>
                                        ${year ? `<div class="movie-year">${year}</div>` : ''}
                                    </div>
                                </a>
                            `;
                        }).join('');
                        
                        mobileSearchResults.scrollTop = 0;
                        
                    } catch (err) {
                        console.error('Mobile search error:', err);
                        mobileSearchResults.innerHTML = `
                            <div class="mobile-search-empty">
                                <i class="fas fa-exclamation-triangle"></i>
                                <p>Đã có lỗi xảy ra. Vui lòng thử lại.</p>
                            </div>
                        `;
                    }
                }, 220);
            });
            
            mobileSearchResults.addEventListener('touchmove', (e) => {
                e.stopPropagation();
            }, { passive: true });
        }
    }
});