// Bộ lọc danh sách phim V2: logic copy nguyên từ Views/Shared/_FilterSidebar.cshtml (V1), tách ra file để tái sử dụng.
    window.toggleFilter = function() {
        const sidebar = document.getElementById('filterSidebar');
        const toggleBtn = document.getElementById('filterToggleBtn');
        const chevron = document.getElementById('filterChevron');
        const backdrop = document.getElementById('filterBackdrop');

        if (sidebar) {
            const isOpen = sidebar.classList.toggle('show');
            if (toggleBtn) { toggleBtn.classList.toggle('active', isOpen); toggleBtn.setAttribute('aria-expanded', isOpen ? 'true' : 'false'); }
            if (chevron) chevron.style.transform = isOpen ? 'rotate(180deg)' : 'rotate(0deg)';
            if (backdrop) backdrop.classList.toggle('is-open', isOpen);
            // Bottom sheet chỉ có ở mobile: khoá cuộn nền khi mở
            if (window.matchMedia('(max-width: 767.98px)').matches) {
                document.body.style.overflow = isOpen ? 'hidden' : '';
                document.body.classList.toggle('v2-sheet-open', isOpen); // ẩn tab bar (z-index tối đa) để không che nút Lọc
            }
        }
    };
    document.addEventListener('keydown', function (e) {
        const sidebar = document.getElementById('filterSidebar');
        if (e.key === 'Escape' && sidebar && sidebar.classList.contains('show')) window.toggleFilter();
    });

    // ===== HÀM MỚI: Xử lý CHỌN NHIỀU (Countries, Categories, Years) =====
    window.selectFilterMulti = function(event, name, value) {
        const form = document.getElementById('filterForm');
        if (!form) return;
        const input = form.querySelector(`input[name="${name}"]`);
        if (!input) return;
        const clickedItem = event.target;

        let valueList = (input.value || "").split(',').filter(Boolean);

        const allButton = clickedItem.parentElement.querySelector('.item');
        if (allButton) allButton.classList.remove('active');

        const index = valueList.indexOf(value);
        if (index > -1) {
            valueList.splice(index, 1);
            clickedItem.classList.remove('active');
        } else {
            valueList.push(value);
            clickedItem.classList.add('active');
        }

        if (valueList.length === 0 && allButton) {
            allButton.classList.add('active');
        }

        input.value = valueList.join(',');

        // 5. Cập nhật ô text nhập năm (KHÔNG CÓN name, dùng querySelector)
        if (name === 'Years') {
            const yearTextInput = document.querySelector('.year-input-container input[type="text"]');
            if (yearTextInput) yearTextInput.value = input.value;
        }
    };

    // ===== HÀM MỚI: Xử lý nút "TẤT CẢ" (cho CHỌN NHIỀU) =====
    window.clearFilterMulti = function(event, name) {
        const form = document.getElementById('filterForm');
        if (!form) return;
        const input = form.querySelector(`input[name="${name}"]`);
        if (!input) return;
        const clickedItem = event.target;

        // 1. Xóa hết giá trị input
        input.value = "";

        // 2. Tắt active tất cả các nút
        const siblings = clickedItem.parentElement.querySelectorAll('.item');
        siblings.forEach(item => {
            item.classList.remove('active');
        });

        // 3. Bật active cho nút "Tất cả"
        clickedItem.classList.add('active');

        // 4. Cập nhật ô text nhập năm (nếu là filter Năm)
        if (name === 'Years') {
            const yearTextInput = document.querySelector('input[name="YearsInput"]');
            if (yearTextInput) yearTextInput.value = "";
        }
    };

    // ===== HÀM CŨ: Xử lý CHỌN MỘT (Type, Language, SortBy) =====
    window.selectFilterSingle = function(event, name, value) {
        const form = document.getElementById('filterForm');
        if (!form) return;
        const input = form.querySelector(`input[name="${name}"]`);

        if (input) {
            input.value = value;
        }

        const activeItem = event.target;
        const siblings = activeItem.parentElement.querySelectorAll('.item');
        siblings.forEach(item => {
            item.classList.remove('active');
        });
        activeItem.classList.add('active');

        // ⭐ Cập nhật form action khi chọn Type
        if (name === 'Type' && typeof window.updateFormAction === 'function') {
            window.updateFormAction();
        }
    };

    // ===== HÀM CŨ: Xử lý khi GÕ VÀO Ô NĂM =====
    function clearFilterButtons(containerElement) {
        // 1. Lấy giá trị từ ô text (KHÔNG CÓN name nữa, dùng querySelector thường)
        const yearTextInput = containerElement.querySelector('input[type="text"]');
        const form = document.getElementById('filterForm');
        const yearsHiddenInput = form.querySelector('input[name="Years"]');

        // 2. Cập nhật input ẩn 'Years'
        yearsHiddenInput.value = yearTextInput.value;

        // 3. Xóa active của tất cả nút 'Năm'
        const siblings = containerElement.closest('.fe-results').querySelectorAll('.item');
        siblings.forEach(item => {
            item.classList.remove('active');
        });

        // 4. Nếu ô text rỗng, bật lại "Tất cả"
        if (yearTextInput.value.length === 0) {
            siblings[0].classList.add('active');
        }
    }
    // ===== Cập nhật form action linh hoạt =====
    function updateFormAction() {
        const form = document.getElementById('filterForm');
        if (!form) return;
        const typeInput = form.querySelector('input[name="Type"]');
        const typeValue = typeInput ? typeInput.value : '';
        const currentPath = window.location.pathname;

        // Nếu người dùng chọn chuyển loại phim rõ ràng khi đang ở trang tìm kiếm hoặc trang chủ
        let action = currentPath;
        if (!currentPath || currentPath === '/' || currentPath === '/trang-chu') {
            if (typeValue === 'single') {
                action = '/the-loai/phim-le';
            } else if (typeValue === 'series') {
                action = '/the-loai/phim-bo';
            } else if (typeValue === 'hoathinh') {
                action = '/the-loai/hoat-hinh';
            } else {
                action = '/tim-kiem';
            }
        }
        form.action = action;
    }

    // Gọi hàm khi trang load
    document.addEventListener('DOMContentLoaded', function () {
        updateFormAction();
    });

    // Cập nhật action trước khi submit
    document.getElementById('filterForm').addEventListener('submit', function (e) {
        updateFormAction();
    });

// ===== V2: select (Năm / Phiên bản / Sắp xếp) cập nhật input ẩn cùng tên, gửi khi bấm "Lọc Phim" =====
window.setFilterFromSelect = function (select, name) {
    const form = document.getElementById('filterForm');
    const input = form && form.querySelector('input[name="' + name + '"]');
    if (input) input.value = select.value;
};

// ===== V2: nút "Xem thêm" mở/thu gọn các hàng chip dài (thể loại, quốc gia) =====
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('[data-collapse-toggle]').forEach(function (toggle) {
        const chips = document.getElementById(toggle.dataset.collapseToggle);
        if (!chips) return;
        const label = toggle.textContent;
        if (chips.scrollHeight <= chips.clientHeight + 4) { toggle.hidden = true; return; }
        toggle.addEventListener('click', function () {
            const open = chips.dataset.open === 'true';
            chips.dataset.open = open ? 'false' : 'true';
            toggle.textContent = open ? label : 'Thu gọn';
        });
    });
});
