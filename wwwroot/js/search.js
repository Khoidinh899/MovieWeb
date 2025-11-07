
document.addEventListener('DOMContentLoaded', function () {
    const input = document.querySelector('.search-box input[name="keyword"]');
    if (!input) return;

    const wrapper = input.closest('.search-input-wrapper');
    wrapper.style.position = wrapper.style.position || 'relative';
    const suggestionBox = document.createElement('div');
    suggestionBox.className = 'search-suggestions shadow';
    suggestionBox.style.position = 'absolute';
    suggestionBox.style.top = '100%';
    suggestionBox.style.left = '0';
    suggestionBox.style.right = '0';
    suggestionBox.style.display = 'none';
    wrapper.appendChild(suggestionBox);

    let timer;
    input.addEventListener('input', function () {
        clearTimeout(timer);
        const q = this.value.trim();
        if (q.length < 2) {
            suggestionBox.style.display = 'none';
            return;
        }
        timer = setTimeout(async () => {
            try {
                const res = await fetch(`/api/goi-y-tim-kiem?keyword=${encodeURIComponent(q)}`);
                const data = await res.json();

                if (!data || data.length === 0) {
                    suggestionBox.innerHTML = '<div class="p-2 text-muted">Không có kết quả</div>';
                    suggestionBox.style.display = 'block';
                    return;
                }

                suggestionBox.innerHTML = data.map(item => {
                    // escape text minimally
                    const name = (item.name || '').replace(/</g,'&lt;').replace(/>/g,'&gt;');
                    const slug = encodeURIComponent(item.slug || '');
                    const img = item.image || '/images/no-image.jpg';
                    return `
                        <a href="/phim/${slug}" class="suggestion-item d-flex align-items-center text-decoration-none p-2">
                            <img src="${img}" onerror="this.src='/images/no-image.jpg'" 
                                 alt="${name}" style="width:48px;height:64px;object-fit:cover;border-radius:4px;margin-right:10px;">
                            <div class="flex-grow-1 text-white text-truncate">${name}</div>
                        </a>
                    `;
                }).join('');

                suggestionBox.style.display = 'block';
            } catch (err) {
                console.error('Search suggest error', err);
                suggestionBox.style.display = 'none';
            }
        }, 220); // debounce
    });

    document.addEventListener('click', (e) => {
        if (!wrapper.contains(e.target)) suggestionBox.style.display = 'none';
    });
});
