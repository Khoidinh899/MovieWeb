// ================== ADMIN REVENUE STATS SCRIPT (ENHANCED & FIXED) ==================

let revenueChart, planRevenueChart;
let currentPage = 1;
let currentFilter = 'all';
const pageSize = 10;

// Load revenue stats on page load
document.addEventListener('DOMContentLoaded', function () {
    console.log('Loading revenue statistics...');
    loadRevenueStats();
    loadRevenueTrend();
    loadPlanRevenue();
    loadRecentTransactions();
});

// ================================================================
// ===== BỘ XỬ LÝ LỖI API VÀ HẾT HẠN COOKIE (ĐÃ THÊM MỚI) =====
// ================================================================

/**
 * Hàm mới: Xử lý tất cả các phản hồi từ fetch
 * Tự động phát hiện lỗi 401 (Hết hạn) và 403 (Cấm)
 */
async function handleApiResponse(response) {
    if (response.ok) {
        // 200 OK - Trả về JSON
        return await response.json();
    }

    if (response.status === 401) {
        // Lỗi 401 - Hết hạn Cookie
        showError('Phiên đăng nhập đã hết hạn. Đang tải lại trang đăng nhập...');
        // Tự động chuyển về trang login sau 3 giây
        setTimeout(() => {
            // Chuyển hướng về trang đăng nhập và đính kèm URL hiện tại để quay lại
            window.location.href = '/Auth/Login?returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
        }, 3000);
        return Promise.reject(new Error('Session expired (401)'));
    }

    if (response.status === 403) {
        // Lỗi 403 - Cấm (Không có quyền Admin)
        showError('Bạn không có quyền thực hiện hành động này (403).');
        return Promise.reject(new Error('Forbidden (403)'));
    }

    // Các lỗi server khác (500, 404, etc.)
    const errorText = await response.text();
    console.error('Server error:', response.status, errorText);
    showError(`Lỗi máy chủ (${response.status}). Vui lòng thử lại.`);
    return Promise.reject(new Error(`Server error (${response.status})`));
}

/**
 * Nâng cấp hàm showError: Dùng "toast" thay vì "alert"
 */
function showError(message) {
    console.error(message);
    
    // Tạo một "toast" alert
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-danger alert-dismissible fade show position-fixed top-0 start-50 translate-middle-x mt-3`;
    alertDiv.style.zIndex = '9999'; // Nằm trên tất cả
    alertDiv.style.minWidth = '300px';
    alertDiv.innerHTML = `
        <i class="bi bi-exclamation-triangle me-2"></i>
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    
    document.body.appendChild(alertDiv);
    
    // Tự động xóa sau 5 giây
    setTimeout(() => {
        // Dùng bootstrap (nếu có) để fade out
        if (typeof bootstrap !== 'undefined') {
            const bsAlert = new bootstrap.Alert(alertDiv);
            bsAlert.close();
        } else {
            alertDiv.remove();
        }
    }, 5000);
}

// ================================================================
// ===== CÁC HÀM GỌI API (ĐÃ SỬA ĐỂ BẮT LỖI 401) =====
// ================================================================

/**
 * Load main revenue statistics with date filter
 */
let currentFilterType = 'month';
let currentStartDate = null;
let currentEndDate = null;

async function loadRevenueStats(startDate = null, endDate = null) {
    try {
        let url = '/api/admin/subscription/stats';

        if (startDate && endDate) {
            url += `?startDate=${startDate}&endDate=${endDate}`;
        }

        const response = await fetch(url, { credentials: 'include' });
        
        // ===== [SỬA LẠI] Dùng hàm xử lý lỗi mới =====
        const result = await handleApiResponse(response);

        // (Code bên dưới chỉ chạy nếu response.ok)
        if (result.success) {
            const { revenue, totalActiveSubscriptions, totalPremiumUsers } = result.data;

            // Update main cards (top 4 cards)
            updateElement('monthlyRevenue', formatCurrency(revenue.totalRevenue));
            updateElement('premiumUsers', totalPremiumUsers);
            updateElement('totalTransactions', revenue.totalTransactions);
            updateElement('activeSubscriptions', totalActiveSubscriptions);

            // Update filter card (new big card)
            updateElement('filterTotalRevenue', formatCurrency(revenue.totalRevenue));
            updateElement('filterCompletedCount', revenue.completedTransactions);
            updateElement('filterPendingCount', revenue.pendingTransactions);
            updateElement('filterFailedCount', revenue.failedTransactions);

            // Add loaded animation class
            document.querySelectorAll('.stat-box, .revenue-stat-box').forEach(box => {
                box.classList.add('loaded');
            });

            console.log('Revenue stats loaded successfully');
        } else {
            // Lỗi logic từ API (ví dụ: success = false)
            showError(result.message || 'Không thể tải thống kê doanh thu');
        }
    } catch (error) {
        // Bắt lỗi (network, hoặc lỗi 401, 500...)
        // Lỗi đã được hiển thị bởi handleApiResponse, chỉ cần log
        console.error('Error in loadRevenueStats:', error.message);
    }
}

/**
 * ✅ FIXED: Load revenue trend chart with smart label formatting
 */
async function loadRevenueTrend(days = 30) {
    try {
        const response = await fetch(`/api/admin/subscription/revenue-trend?days=${days}`, {
            credentials: 'include'
        });
        
        // ===== [SỬA LẠI] Dùng hàm xử lý lỗi mới =====
        const result = await handleApiResponse(response);

        if (result.success && result.data && result.data.length > 0) {
            // ... (toàn bộ code vẽ biểu đồ của bạn giữ nguyên) ...
            
            // ✅ Smart label formatting based on number of days
            let labels;
            if (days === 1) {
                // Hôm nay: Hiển thị giờ (00:00, 06:00, 12:00...)
                labels = result.data.map(d => {
                    const date = new Date(d.date);
                    return `${date.getHours().toString().padStart(2, '0')}:00`;
                });
            } else if (days <= 7) {
                // Tuần: Hiển thị ngày + thứ (T2, T3...)
                labels = result.data.map(d => {
                    const date = new Date(d.date);
                    const dayNames = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
                    return `${dayNames[date.getDay()]} ${date.getDate()}/${date.getMonth() + 1}`;
                });
            } else if (days <= 31) {
                // Tháng: Hiển thị dd/MM
                labels = result.data.map(d => {
                    const date = new Date(d.date);
                    return `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}`;
                });
            } else if (days <= 90) {
                // 3 tháng: Hiển thị dd/MM
                labels = result.data.map(d => {
                    const date = new Date(d.date);
                    return `${date.getDate()}/${date.getMonth() + 1}`;
                });
            } else {
                // Năm: Hiển thị tháng (T1, T2...)
                labels = result.data.map(d => {
                    const date = new Date(d.date);
                    return `T${date.getMonth() + 1}`;
                });
            }

            const data = result.data.map(d => d.revenue);

            const ctx = document.getElementById('revenueChart');
            if (!ctx) {
                console.warn('Revenue chart canvas not found');
                return;
            }

            // Destroy existing chart
            if (revenueChart) {
                revenueChart.destroy();
            }

            // ✅ Calculate optimal tick limits based on data length
            const maxTicksLimit = days <= 7 ? days : days <= 31 ? 15 : days <= 90 ? 12 : 12;

            // (Toàn bộ code `new Chart(...)` của bạn giữ nguyên)
            revenueChart = new Chart(ctx.getContext('2d'), {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Doanh Thu',
                        data: data,
                        borderColor: '#667eea',
                        backgroundColor: 'rgba(102, 126, 234, 0.1)',
                        tension: 0.4,
                        fill: true,
                        pointRadius: days <= 7 ? 4 : days <= 31 ? 3 : 2,
                        pointHoverRadius: 6,
                        pointBackgroundColor: '#667eea',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: true,
                            position: 'top',
                            labels: {
                                boxWidth: 12,
                                font: {
                                    size: 12
                                }
                            }
                        },
                        tooltip: {
                            backgroundColor: 'rgba(0, 0, 0, 0.8)',
                            padding: 12,
                            titleFont: {
                                size: 13
                            },
                            bodyFont: {
                                size: 12
                            },
                            callbacks: {
                                label: function (context) {
                                    return 'Doanh thu: ' + formatCurrencyShort(context.parsed.y);
                                }
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function (value) {
                                    if (value >= 1000000) {
                                        return (value / 1000000).toFixed(1) + 'M';
                                    } else if (value >= 1000) {
                                        return (value / 1000).toFixed(0) + 'K';
                                    }
                                    return value;
                                },
                                font: {
                                    size: 11
                                },
                                maxTicksLimit: 6,
                                padding: 8
                            },
                            grid: {
                                color: 'rgba(0, 0, 0, 0.05)',
                                drawBorder: false
                            }
                        },
                        x: {
                            ticks: {
                                font: {
                                    size: 10
                                },
                                maxRotation: days <= 7 ? 0 : 45, // ✅ Không xoay nếu ít ngày
                                minRotation: days <= 7 ? 0 : 45,
                                padding: 5,
                                autoSkip: true,
                                maxTicksLimit: maxTicksLimit
                            },
                            grid: {
                                display: false
                            }
                        }
                    },
                    layout: {
                        padding: {
                            left: 10,
                            right: 10,
                            top: 10,
                            bottom: 10
                        }
                    }
                }
            });

            console.log(`✅ Revenue trend chart loaded successfully (${days} days)`);
        } else {
            console.warn('No revenue trend data available');
        }
    } catch (error) {
        console.error('Error in loadRevenueTrend:', error.message);
    }
}

/**
 * ✅ FIXED: Load plan revenue chart (doughnut) - Better layout
 */
async function loadPlanRevenue() {
    try {
        const response = await fetch('/api/admin/subscription/revenue-by-plan', {
            credentials: 'include'
        });
        
        // ===== [SỬA LẠI] Dùng hàm xử lý lỗi mới =====
        const result = await handleApiResponse(response);

        if (result.success && result.data && result.data.length > 0) {
            // ... (toàn bộ code vẽ biểu đồ của bạn giữ nguyên) ...
            
            const labels = result.data.map(d => d.displayName);
            const data = result.data.map(d => d.totalRevenue);
            const colors = [
                '#667eea',
                '#764ba2',
                '#f093fb',
                '#f5576c',
                '#4facfe',
                '#00f2fe',
                '#43e97b',
                '#38f9d7'
            ];

            const ctx = document.getElementById('planRevenueChart');
            if (!ctx) {
                console.warn('Plan revenue chart canvas not found');
                return;
            }

            // Destroy existing chart
            if (planRevenueChart) {
                planRevenueChart.destroy();
            }

            // (Toàn bộ code `new Chart(...)` của bạn giữ nguyên)
            planRevenueChart = new Chart(ctx.getContext('2d'), {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: data,
                        backgroundColor: colors,
                        borderWidth: 3,
                        borderColor: '#fff',
                        hoverBorderWidth: 4,
                        hoverOffset: 10
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false, // ✅ Important
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                padding: 12,
                                font: {
                                    size: 11
                                },
                                boxWidth: 15,
                                boxHeight: 15,
                                usePointStyle: true,
                                pointStyle: 'circle'
                            }
                        },
                        tooltip: {
                            backgroundColor: 'rgba(0, 0, 0, 0.8)',
                            padding: 12,
                            titleFont: {
                                size: 13
                            },
                            bodyFont: {
                                size: 12
                            },
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.parsed;
                                    const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                    const percentage = ((value / total) * 100).toFixed(1);
                                    return `${label}: ${formatCurrencyShort(value)} (${percentage}%)`;
                                }
                            }
                        }
                    },
                    layout: {
                        padding: {
                            top: 10,
                            bottom: 10
                        }
                    }
                }
            });

            console.log('✅ Plan revenue chart loaded successfully');
        } else {
            console.warn('No plan revenue data available');
        }
    } catch (error) {
        console.error('Error in loadPlanRevenue:', error.message);
    }
}

/**
 * Load recent transactions table with filter and pagination
 */
async function loadRecentTransactions(page = 1, status = 'all') {
    try {
        currentPage = page;
        currentFilter = status;

        const statusParam = status !== 'all' ? `&status=${status}` : '';

        const response = await fetch(`/api/admin/subscription/all-transactions?page=${page}&pageSize=${pageSize}${statusParam}`, {
            credentials: 'include'
        });

        // ===== [SỬA LẠI] Dùng hàm xử lý lỗi mới =====
        const result = await handleApiResponse(response);

        if (result.success && result.data && result.data.items) {
            const tbody = document.querySelector('#recentTransactionsTable tbody');
            if (!tbody) {
                console.warn('Transactions table body not found');
                return;
            }

            tbody.innerHTML = '';

            if (result.data.items.length === 0) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center text-muted py-4">
                            <i class="bi bi-inbox fs-3"></i>
                            <p class="mt-2 mb-0">Không có giao dịch nào</p>
                        </td>
                    </tr>
                `;
                updatePaginationInfo(0, 0);
                renderPagination(0, 1); // Fix: Hiển thị 0 trang
                return;
            }

            result.data.items.forEach(tx => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td><code>${escapeHtml(tx.transactionCode)}</code></td>
                    <td>
                        <div class="d-flex align-items-center">
                            <i class="bi bi-person-circle me-2"></i>
                            <span>${escapeHtml(tx.userName || `User #${tx.userId}`)}</span>
                        </div>
                    </td>
                    <td>${tx.plan ? escapeHtml(tx.plan.displayName) : '<span class="text-muted">N/A</span>'}</td>
                    <td><strong>${escapeHtml(tx.amountDisplay)}</strong></td>
                    <td><span class="badge bg-secondary">${escapeHtml(tx.paymentMethod)}</span></td>
                    <td><span class="badge bg-${getStatusColor(tx.status)}">${escapeHtml(tx.statusDisplay)}</span></td>
                    <td>${formatDateTime(tx.createdAt)}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-primary" onclick="viewTransactionDetails(${tx.transactionId})" title="Xem chi tiết">
                            <i class="bi bi-eye"></i>
                        </button>
                    </td>
                `;
                tbody.appendChild(row);
            });

            updatePaginationInfo(result.data.items.length, result.data.totalItems);
            renderPagination(result.data.totalPages, page);

            console.log('✅ Recent transactions loaded successfully');
        } else {
            // Trường hợp `result.success` là `false`
            throw new Error(result.message || 'Failed to load transactions');
        }
    } catch (error) {
        console.error('Error in loadRecentTransactions:', error.message);
        // Lỗi 401, 500... đã được handleApiResponse xử lý
        // Chỉ xử lý lỗi giao diện nếu là lỗi network
        if (!error.message.includes('401') && !error.message.includes('Server error')) {
            const tbody = document.querySelector('#recentTransactionsTable tbody');
            if (tbody) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center text-danger py-4">
                            <i class="bi bi-exclamation-triangle fs-3"></i>
                            <p class="mt-2 mb-0">Lỗi tải dữ liệu. Vui lòng kiểm tra kết nối.</p>
                        </td>
                    </tr>
                `;
            }
        }
    }
}

/**
 * Filter transactions by status
 */
function filterTransactions(status) {
    document.querySelectorAll('.btn-group button').forEach(btn => {
        btn.classList.remove('active');
    });
    event.target.classList.add('active');
    loadRecentTransactions(1, status);
}

/**
 * Update pagination info
 */
function updatePaginationInfo(showing, total) {
    const showingEl = document.getElementById('showingCount');
    const totalEl = document.getElementById('totalCount');
    if (showingEl) showingEl.textContent = showing;
    if (totalEl) totalEl.textContent = total;
}

/**
 * Render pagination controls
 */
function renderPagination(totalPages, currentPage) {
    const container = document.getElementById('paginationControls');
    if (!container) return;

    if (totalPages <= 1) {
        container.innerHTML = '';
        return;
    }

    let html = '<nav><ul class="pagination pagination-sm mb-0">';

    html += `
        <li class="page-item ${currentPage === 1 ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage - 1}); return false;">
                <i class="bi bi-chevron-left"></i>
            </a>
        </li>
    `;

    const maxVisible = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisible / 2));
    let endPage = Math.min(totalPages, startPage + maxVisible - 1);

    if (endPage - startPage < maxVisible - 1) {
        startPage = Math.max(1, endPage - maxVisible + 1);
    }

    if (startPage > 1) {
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(1); return false;">1</a></li>`;
        if (startPage > 2) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
    }

    for (let i = startPage; i <= endPage; i++) {
        html += `
            <li class="page-item ${i === currentPage ? 'active' : ''}">
                <a class="page-link" href="#" onclick="changePage(${i}); return false;">${i}</a>
            </li>
        `;
    }

    if (endPage < totalPages) {
        if (endPage < totalPages - 1) {
            html += `<li class="page-item disabled"><span class="page-link">...</span></li>`;
        }
        html += `<li class="page-item"><a class="page-link" href="#" onclick="changePage(${totalPages}); return false;">${totalPages}</a></li>`;
    }

    html += `
        <li class="page-item ${currentPage === totalPages ? 'disabled' : ''}">
            <a class="page-link" href="#" onclick="changePage(${currentPage + 1}); return false;">
                <i class="bi bi-chevron-right"></i>
            </a>
        </li>
    `;

    html += '</ul></nav>';
    container.innerHTML = html;
}

function changePage(page) {
    loadRecentTransactions(page, currentFilter);
}

/**
 * View transaction details (modal)
 */
async function viewTransactionDetails(transactionId) {
    try {
        // [SỬA LẠI] API này không có phân trang, nhưng ta vẫn dùng hàm check lỗi
        const response = await fetch(`/api/admin/subscription/all-transactions?page=1&pageSize=100`, {
            credentials: 'include'
        });
        
        // ===== [SỬA LẠI] Dùng hàm xử lý lỗi mới =====
        const result = await handleApiResponse(response);

        if (result.success && result.data) {
            const transaction = result.data.items.find(t => t.transactionId === transactionId);

            if (!transaction) {
                showError('Không tìm thấy giao dịch');
                return;
            }

            // (Code HTML của modal giữ nguyên)
            const modalHtml = `
                <div class="modal fade" id="transactionModal" tabindex="-1">
                    <div class="modal-dialog modal-lg modal-dialog-centered">
                        <div class="modal-content shadow-lg border-0 rounded-4">
                            <div class="modal-header bg-light border-bottom">
                                <h5 class="modal-title fw-bold text-primary">
                                    <i class="bi bi-receipt me-2"></i>Chi Tiết Giao Dịch
                                </h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body px-4 py-3">
                                <div class="row gy-3 gx-4">
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Mã Giao Dịch</label>
                                        <div class="fw-bold text-danger"><code>${escapeHtml(transaction.transactionCode)}</code></div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Trạng Thái</label>
                                        <div><span class="badge bg-${getStatusColor(transaction.status)} px-3 py-2">${escapeHtml(transaction.statusDisplay)}</span></div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Số Tiền</label>
                                        <div class="fw-bold fs-5">${escapeHtml(transaction.amountDisplay)}</div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Phương Thức</label>
                                        <div><span class="badge bg-secondary px-3 py-2">${escapeHtml(transaction.paymentMethod)}</span></div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Người Dùng</label>
                                        <div class="fw-semibold">${escapeHtml(transaction.userName)} (#${transaction.userId})</div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Gói</label>
                                        <div>${transaction.plan ? escapeHtml(transaction.plan.displayName) : '<span class="text-muted">N/A</span>'}</div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Ngày Tạo</label>
                                        <div>${formatDateTime(transaction.createdAt)}</div>
                                    </div>
                                    <div class="col-md-6">
                                        <label class="form-label text-muted mb-1">Ngày Hoàn Thành</label>
                                        <div>${transaction.completedAt ? formatDateTime(transaction.completedAt) : '<span class="text-muted">Chưa hoàn thành</span>'}</div>
                                    </div>
                                    ${transaction.description ? `
                                    <div class="col-12">
                                        <label class="form-label text-muted mb-1">Mô Tả</label>
                                        <div>${escapeHtml(transaction.description)}</div>
                                    </div>` : ''}
                                </div>
                            </div>
                            <div class="modal-footer bg-light border-0">
                                <button type="button" class="btn btn-secondary px-4" data-bs-dismiss="modal">Đóng</button>
                            </div>
                        </div>
                    </div>
                </div>
            `;

            document.body.insertAdjacentHTML('beforeend', modalHtml);
            const modal = new bootstrap.Modal(document.getElementById('transactionModal'));
            modal.show();

            document.getElementById('transactionModal').addEventListener('hidden.bs.modal', () => {
                document.getElementById('transactionModal').remove();
            });
        }
    } catch (error) {
        console.error('Error in viewTransactionDetails:', error.message);
    }
}

/**
 * ✅ FIXED: Refresh with correct days based on current filter type
 */
function refreshRevenueStats() {
    console.log('Refreshing revenue statistics...');

    const refreshBtn = document.querySelector('[onclick="refreshRevenueStats()"]');
    if (refreshBtn) {
        const icon = refreshBtn.querySelector('i');
        if (icon) icon.classList.add('spinning');
    }

    // ✅ Tính đúng số ngày dựa trên filter type
    let chartDays = 30;
    if (currentFilterType === 'today') {
        chartDays = 1;
    } else if (currentFilterType === 'week') {
        chartDays = 7;
    } else if (currentFilterType === 'year') {
        chartDays = 365;
    } else if (currentFilterType === 'custom' && currentStartDate && currentEndDate) {
        const start = new Date(currentStartDate);
        const end = new Date(currentEndDate);
        chartDays = Math.ceil((end - start) / (1000 * 60 * 60 * 24)) + 1;
    }

    Promise.all([
        loadRevenueStats(currentStartDate, currentEndDate),
        loadRevenueTrend(chartDays), // ✅ Pass đúng số ngày
        loadPlanRevenue(),
        loadRecentTransactions(currentPage, currentFilter)
    ]).then(() => {
        console.log('All revenue stats refreshed');
        if (refreshBtn) {
            const icon = refreshBtn.querySelector('i');
            if (icon) icon.classList.remove('spinning');
        }
    }).catch(error => {
        console.error('Error refreshing stats:', error);
    });
}

/**
 * ✅ FIXED: Filter revenue by predefined periods with correct chart days
 */
function filterRevenue(type) {
    currentFilterType = type;

    document.querySelectorAll('.revenue-filter-card .btn-group .btn').forEach(btn => {
        btn.classList.remove('active');
    });
    event.target.closest('button').classList.add('active');

    const now = new Date();
    let startDate, endDate, periodText, chartDays;

    switch (type) {
        case 'today':
            startDate = new Date(now.getFullYear(), now.getMonth(), now.getDate());
            endDate = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59);
            periodText = 'Hôm nay';
            chartDays = 1; // ✅ Chỉ hiển thị hôm nay
            break;
        case 'week':
            const firstDayOfWeek = now.getDate() - now.getDay();
            startDate = new Date(now.getFullYear(), now.getMonth(), firstDayOfWeek);
            endDate = new Date(now.getFullYear(), now.getMonth(), firstDayOfWeek + 6, 23, 59, 59);
            periodText = 'Tuần này';
            chartDays = 7; // ✅ 7 ngày
            break;
        case 'month':
            startDate = new Date(now.getFullYear(), now.getMonth(), 1);
            endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59);
            periodText = 'Tháng này';
            chartDays = 30; // ✅ 30 ngày
            break;
        case 'year':
            startDate = new Date(now.getFullYear(), 0, 1);
            endDate = new Date(now.getFullYear(), 11, 31, 23, 59, 59);
            periodText = 'Năm này';
            chartDays = 365; // ✅ 365 ngày (hoặc dùng 12 months nếu backend hỗ trợ)
            break;
        default:
            startDate = new Date(now.getFullYear(), now.getMonth(), 1);
            endDate = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59);
            periodText = 'Tháng này';
            chartDays = 30;
    }

    currentStartDate = startDate.toISOString();
    currentEndDate = endDate.toISOString();

    updateElement('filterPeriod', periodText);
    loadRevenueStats(currentStartDate, currentEndDate);
    loadRevenueTrend(chartDays); // ✅ Pass đúng số ngày
}

function showCustomDatePicker() {
    const modal = new bootstrap.Modal(document.getElementById('customDateModal'));
    const now = new Date();
    const firstDayOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);
    document.getElementById('customStartDate').valueAsDate = firstDayOfMonth;
    document.getElementById('customEndDate').valueAsDate = now;
    modal.show();
}

/**
 * ✅ FIXED: Apply custom date filter with calculated days
 */
function applyCustomDateFilter() {
    const startDateInput = document.getElementById('customStartDate').value;
    const endDateInput = document.getElementById('customEndDate').value;

    if (!startDateInput || !endDateInput) {
        alert('Vui lòng chọn đầy đủ ngày bắt đầu và kết thúc');
        return;
    }

    const startDate = new Date(startDateInput);
    const endDate = new Date(endDateInput);

    if (startDate > endDate) {
        alert('Ngày bắt đầu phải nhỏ hơn ngày kết thúc');
        return;
    }

    document.querySelectorAll('.revenue-filter-card .btn-group .btn').forEach(btn => {
        btn.classList.remove('active');
    });
    document.querySelector('.revenue-filter-card .btn-group .btn:last-child').classList.add('active');

    currentFilterType = 'custom';
    currentStartDate = startDate.toISOString();
    currentEndDate = new Date(endDate.getFullYear(), endDate.getMonth(), endDate.getDate(), 23, 59, 59).toISOString();

    const periodText = `${formatDate(startDate)} - ${formatDate(endDate)}`;
    updateElement('filterPeriod', periodText);

    bootstrap.Modal.getInstance(document.getElementById('customDateModal')).hide();

    loadRevenueStats(currentStartDate, currentEndDate);
    
    // ✅ Tính số ngày chính xác
    const diffDays = Math.ceil((endDate - startDate) / (1000 * 60 * 60 * 24)) + 1; // +1 để include cả ngày cuối
    loadRevenueTrend(diffDays);
}

// ================== UTILITY FUNCTIONS (Giữ nguyên) ==================

/**
 * ✅ NEW: Format currency SHORT for charts (3M, 500K)
 */
function formatCurrencyShort(value) {
    if (value === null || value === undefined) return '0';
    if (value >= 1000000000) {
        return (value / 1000000000).toFixed(1) + 'B';
    } else if (value >= 1000000) {
        return (value / 1000000).toFixed(1) + 'M';
    } else if (value >= 1000) {
        return (value / 1000).toFixed(0) + 'K';
    }
    return value.toString();
}

function formatCurrency(value) {
    if (value === null || value === undefined) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(value);
}

function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    });
}

function formatDateTime(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function getStatusColor(status) {
    const colors = {
        'completed': 'success',
        'pending': 'warning',
        'failed': 'danger',
        'refunded': 'info',
        'cancelled': 'secondary'
    };
    return colors[status] || 'secondary';
}

function updateElement(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value;
    }
}

function escapeHtml(text) {
    if (!text) return '';
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.toString().replace(/[&<>"']/g, m => map[m]);
}

// ================== ADD SPINNING ANIMATION STYLE (Giữ nguyên) ==================

if (!document.getElementById('spinning-style')) {
    const style = document.createElement('style');
    style.id = 'spinning-style';
    style.textContent = `
        @keyframes spin {
            from { transform: rotate(0deg); }
            to { transform: rotate(360deg); }
        }
        .spinning {
            animation: spin 0.5s linear;
        }
    `;
    document.head.appendChild(style);
}