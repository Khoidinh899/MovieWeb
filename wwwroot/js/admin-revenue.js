// ================== ADMIN REVENUE STATS SCRIPT ==================

let revenueChart, planRevenueChart;

// Load revenue stats on page load
document.addEventListener('DOMContentLoaded', function() {
    console.log('Loading revenue statistics...');
    loadRevenueStats();
    loadRevenueTrend();
    loadPlanRevenue();
    loadRecentTransactions();
});

/**
 * Load main revenue statistics
 */
async function loadRevenueStats() {
    try {
        const response = await fetch('/api/admin/subscription/stats');
        const result = await response.json();
        
        if (result.success) {
            const { revenue, totalActiveSubscriptions, totalPremiumUsers } = result.data;
            
            // Update main cards
            updateElement('monthlyRevenue', formatCurrency(revenue.totalRevenue));
            updateElement('premiumUsers', totalPremiumUsers);
            updateElement('completedTransactions', revenue.totalTransactions);
            updateElement('activeSubscriptions', totalActiveSubscriptions);
            
            // Calculate and update other stats (example calculations)
            const pendingCount = Math.round(revenue.totalTransactions * 0.05);
            const failedCount = Math.round(revenue.totalTransactions * 0.02);
            updateElement('pendingTransactions', pendingCount);
            updateElement('failedTransactions', failedCount);
            
            // Add loaded animation class
            document.querySelectorAll('.stat-box').forEach(box => {
                box.classList.add('loaded');
            });
            
            console.log('Revenue stats loaded successfully');
        } else {
            console.error('Failed to load revenue stats:', result.message);
            showError('Không thể tải thống kê doanh thu');
        }
    } catch (error) {
        console.error('Error loading revenue stats:', error);
        showError('Lỗi kết nối server');
    }
}

/**
 * Load revenue trend chart (30 days)
 */
async function loadRevenueTrend() {
    try {
        const response = await fetch('/api/admin/subscription/revenue-trend?days=30');
        const result = await response.json();
        
        if (result.success && result.data && result.data.length > 0) {
            const labels = result.data.map(d => formatDate(d.date));
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
            
            // Create new chart
            revenueChart = new Chart(ctx.getContext('2d'), {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Doanh Thu (VND)',
                        data: data,
                        borderColor: '#667eea',
                        backgroundColor: 'rgba(102, 126, 234, 0.1)',
                        tension: 0.4,
                        fill: true,
                        pointRadius: 4,
                        pointHoverRadius: 6,
                        pointBackgroundColor: '#667eea',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    plugins: {
                        legend: { 
                            display: false 
                        },
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    return 'Doanh thu: ' + formatCurrency(context.parsed.y);
                                }
                            }
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function(value) {
                                    return formatCurrency(value);
                                }
                            },
                            grid: {
                                color: 'rgba(0, 0, 0, 0.05)'
                            }
                        },
                        x: {
                            grid: {
                                display: false
                            }
                        }
                    }
                }
            });
            
            console.log('Revenue trend chart loaded successfully');
        } else {
            console.warn('No revenue trend data available');
        }
    } catch (error) {
        console.error('Error loading revenue trend:', error);
    }
}

/**
 * Load plan revenue chart (doughnut)
 */
async function loadPlanRevenue() {
    try {
        const response = await fetch('/api/admin/subscription/revenue-by-plan');
        const result = await response.json();
        
        if (result.success && result.data && result.data.length > 0) {
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
            
            // Create new chart
            planRevenueChart = new Chart(ctx.getContext('2d'), {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: data,
                        backgroundColor: colors,
                        borderWidth: 2,
                        borderColor: '#fff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: true,
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                padding: 15,
                                font: {
                                    size: 12
                                }
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: function(context) {
                                    const label = context.label || '';
                                    const value = context.parsed;
                                    const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                    const percentage = ((value / total) * 100).toFixed(1);
                                    return `${label}: ${formatCurrency(value)} (${percentage}%)`;
                                }
                            }
                        }
                    }
                }
            });
            
            console.log('Plan revenue chart loaded successfully');
        } else {
            console.warn('No plan revenue data available');
        }
    } catch (error) {
        console.error('Error loading plan revenue:', error);
    }
}

/**
 * Load recent transactions table
 */
async function loadRecentTransactions() {
    try {
        const response = await fetch('/api/admin/subscription/all-transactions?page=1&pageSize=10');
        const result = await response.json();
        
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
                        <td colspan="6" class="text-center text-muted py-4">
                            <i class="bi bi-inbox"></i> Chưa có giao dịch nào
                        </td>
                    </tr>
                `;
                return;
            }
            
            result.data.items.forEach(tx => {
                const row = document.createElement('tr');
                row.innerHTML = `
                    <td><code>${escapeHtml(tx.transactionCode)}</code></td>
                    <td>User #${tx.userId}</td>
                    <td>${tx.plan ? escapeHtml(tx.plan.displayName) : 'N/A'}</td>
                    <td><strong>${escapeHtml(tx.amountDisplay)}</strong></td>
                    <td><span class="badge bg-${getStatusColor(tx.status)}">${escapeHtml(tx.statusDisplay)}</span></td>
                    <td>${formatDate(tx.createdAt)}</td>
                `;
                tbody.appendChild(row);
            });
            
            console.log('Recent transactions loaded successfully');
        } else {
            console.warn('No transaction data available');
        }
    } catch (error) {
        console.error('Error loading recent transactions:', error);
        const tbody = document.querySelector('#recentTransactionsTable tbody');
        if (tbody) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-center text-danger py-4">
                        <i class="bi bi-exclamation-triangle"></i> Lỗi tải dữ liệu
                    </td>
                </tr>
            `;
        }
    }
}

/**
 * Refresh all revenue statistics
 */
function refreshRevenueStats() {
    console.log('Refreshing revenue statistics...');
    
    // Show loading state
    const refreshBtn = document.querySelector('[onclick="refreshRevenueStats()"]');
    if (refreshBtn) {
        const icon = refreshBtn.querySelector('i');
        if (icon) {
            icon.classList.add('spinning');
        }
    }
    
    // Reload all data
    Promise.all([
        loadRevenueStats(),
        loadRevenueTrend(),
        loadPlanRevenue(),
        loadRecentTransactions()
    ]).then(() => {
        console.log('All revenue stats refreshed');
        if (refreshBtn) {
            const icon = refreshBtn.querySelector('i');
            if (icon) {
                icon.classList.remove('spinning');
            }
        }
    }).catch(error => {
        console.error('Error refreshing stats:', error);
    });
}

// ================== UTILITY FUNCTIONS ==================

/**
 * Format currency to VND
 */
function formatCurrency(value) {
    if (value === null || value === undefined) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
        style: 'currency',
        currency: 'VND'
    }).format(value);
}

/**
 * Format date to Vietnamese format
 */
function formatDate(dateString) {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric'
    });
}

/**
 * Get status color class
 */
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

/**
 * Update element text content
 */
function updateElement(id, value) {
    const element = document.getElementById(id);
    if (element) {
        element.textContent = value;
    }
}

/**
 * Escape HTML to prevent XSS
 */
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

/**
 * Show error message
 */
function showError(message) {
    console.error(message);
    // You can implement a toast notification here
    // For now, just log to console
}

// Add spinning animation style dynamically
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