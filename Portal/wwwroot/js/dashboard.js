// Dashboard JavaScript with Enhanced Error Handling
class DashboardManager {
    constructor() {
        this.charts = new Map();
        this.isInitialized = false;
        this.autoRefreshInterval = null;
        this.config = {
            autoRefresh: true,
            refreshInterval: 120000,
            chartAnimation: true,
            lazyLoad: true
        };

        this.colors = {
            status: {
                'Available': '#059669',
                'In Use': '#2563eb',
                'Under Maintenance': '#d97706',
                'Retired': '#71717a',
                'Lost': '#e11d48',
                'Unknown': '#6b7280',
                'No Data': '#9ca3af'
            },
            condition: {
                'New': '#059669',
                'Good': '#2563eb',
                'Fair': '#d97706',
                'Poor': '#f59e0b',
                'Broken': '#e11d48',
                'Unknown': '#6b7280',
                'No Data': '#9ca3af'
            },
            trends: {
                value: '#2563eb',
                count: '#059669',
                depreciation: '#71717a'
            }
        };
    }

    initialize() {
        if (this.isInitialized) {
            console.log('Dashboard already initialized');
            return;
        }

        console.log('Initializing dashboard...');

        // Check if Chart.js is available
        if (typeof Chart === 'undefined') {
            console.error('Chart.js is not loaded. Please include Chart.js in your page.');
            this.showToast('Charts library not loaded. Please refresh the page.', 'error');
            return;
        }

        try {
            this.initializeCharts();
            this.setupEventListeners();
            this.startAutoRefresh();
            this.setupLazyLoading();

            this.isInitialized = true;
            console.log('Dashboard initialized successfully');
        } catch (error) {
            console.error('Failed to initialize dashboard:', error);
            this.showToast('Failed to initialize dashboard charts', 'error');
        }
    }

    initializeCharts() {
        console.log('Initializing charts...');

        // Initialize each chart with error handling
        const chartInitializers = [
            { name: 'status', initializer: () => this.createStatusChart() },
            { name: 'condition', initializer: () => this.createConditionChart() },
            { name: 'trend', initializer: () => this.createTrendChart() }
        ];

        chartInitializers.forEach(({ name, initializer }) => {
            try {
                initializer();
                console.log(`✅ ${name} chart initialized`);
            } catch (error) {
                console.error(`❌ Failed to initialize ${name} chart:`, error);
                this.showToast(`Failed to load ${name} chart`, 'error');
            }
        });
    }

    createStatusChart() {
        const canvas = document.getElementById('statusChart');
        if (!canvas) {
            throw new Error('Status chart canvas element not found');
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            throw new Error('Could not get 2D context for status chart');
        }

        const statusData = window.chartData?.statusData || [];
        console.log('Status chart data:', statusData);

        // Ensure we have data
        if (!statusData || statusData.length === 0) {
            this.createEmptyChart(ctx, 'No status data available');
            return;
        }

        const total = statusData.reduce((sum, item) => sum + (item.count || 0), 0);

        this.charts.set('status', new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: statusData.map(s => {
                    const percentage = total > 0 ? ((s.count / total) * 100).toFixed(1) : '0';
                    return `${s.status} (${percentage}%)`;
                }),
                datasets: [{
                    data: statusData.map(s => s.count || 0),
                    backgroundColor: statusData.map(s => this.colors.status[s.status] || '#6b7280'),
                    borderWidth: 2,
                    borderColor: '#ffffff',
                    hoverBorderWidth: 3,
                    hoverOffset: 8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 15,
                            usePointStyle: true,
                            font: {
                                size: 11,
                                family: "system-ui, -apple-system, sans-serif"
                            },
                            color: '#374151'
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(255, 255, 255, 0.95)',
                        titleColor: '#111827',
                        bodyColor: '#374151',
                        borderColor: '#e5e7eb',
                        borderWidth: 1,
                        cornerRadius: 6,
                        padding: 8,
                        callbacks: {
                            label: (context) => {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
                                return `${label.split(' (')[0]}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                cutout: '65%',
                animation: this.config.chartAnimation ? {
                    animateScale: true,
                    animateRotate: true,
                    duration: 800
                } : false
            }
        }));
    }

    createConditionChart() {
        const canvas = document.getElementById('conditionChart');
        if (!canvas) {
            throw new Error('Condition chart canvas element not found');
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            throw new Error('Could not get 2D context for condition chart');
        }

        const conditionData = window.chartData?.conditionData || [];
        console.log('Condition chart data:', conditionData);

        if (!conditionData || conditionData.length === 0) {
            this.createEmptyChart(ctx, 'No condition data available');
            return;
        }

        const total = conditionData.reduce((sum, item) => sum + (item.count || 0), 0);

        this.charts.set('condition', new Chart(ctx, {
            type: 'pie',
            data: {
                labels: conditionData.map(c => c.condition),
                datasets: [{
                    data: conditionData.map(c => c.count || 0),
                    backgroundColor: conditionData.map(c => this.colors.condition[c.condition] || '#6b7280'),
                    borderWidth: 2,
                    borderColor: '#ffffff',
                    hoverBorderWidth: 3,
                    hoverOffset: 8
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 15,
                            usePointStyle: true,
                            font: {
                                size: 11
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: (context) => {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                animation: this.config.chartAnimation ? {
                    animateScale: true,
                    animateRotate: true,
                    duration: 800
                } : false
            }
        }));
    }

    createTrendChart() {
        const canvas = document.getElementById('monthlyTrendChart');
        if (!canvas) {
            throw new Error('Trend chart canvas element not found');
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            throw new Error('Could not get 2D context for trend chart');
        }

        const monthlyData = window.chartData?.monthlyData || [];
        console.log('Trend chart data:', monthlyData);

        if (!monthlyData || monthlyData.length === 0) {
            this.createEmptyChart(ctx, 'No trend data available', 'line');
            return;
        }

        this.charts.set('trend', new Chart(ctx, {
            type: 'line',
            data: {
                labels: monthlyData.map(m => m.month),
                datasets: [{
                    label: 'Asset Value',
                    data: monthlyData.map(m => m.value || 0),
                    borderColor: this.colors.trends.value,
                    backgroundColor: this.hexToRgba(this.colors.trends.value, 0.1),
                    borderWidth: 2,
                    fill: true,
                    tension: 0.3,
                    pointBackgroundColor: this.colors.trends.value,
                    pointBorderColor: '#ffffff',
                    pointBorderWidth: 2,
                    pointRadius: 3,
                    pointHoverRadius: 5
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    }
                },
                scales: {
                    y: {
                        beginAtZero: false,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            callback: (value) => {
                                return '₵' + value.toLocaleString('en-GH', {
                                    minimumFractionDigits: 0,
                                    maximumFractionDigits: 0
                                });
                            }
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                },
                interaction: {
                    intersect: false,
                    mode: 'index'
                }
            }
        }));
    }

    createEmptyChart(ctx, message, type = 'doughnut') {
        console.warn(`Creating empty chart: ${message}`);

        if (type === 'doughnut' || type === 'pie') {
            this.charts.set('empty', new Chart(ctx, {
                type: type,
                data: {
                    labels: ['No Data'],
                    datasets: [{
                        data: [1],
                        backgroundColor: ['#f3f4f6'],
                        borderWidth: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            enabled: false
                        }
                    },
                    animation: false
                }
            }));

            // Add text annotation
            ctx.font = '14px system-ui';
            ctx.fillStyle = '#6b7280';
            ctx.textAlign = 'center';
            ctx.fillText(message, ctx.canvas.width / 2, ctx.canvas.height / 2);
        }
    }

    // ... rest of your existing methods (changeChartView, setupEventListeners, etc.)
    changeChartView(type) {
        const trendChart = this.charts.get('trend');
        if (!trendChart) {
            console.warn('Trend chart not available for view change');
            return;
        }

        const monthlyData = window.chartData?.monthlyData || [];
        const buttons = document.querySelectorAll('.btn-group .btn');

        buttons.forEach(btn => btn.classList.remove('active'));
        if (event && event.target) {
            event.target.classList.add('active');
        }

        let data, label, callback, borderColor, backgroundColor;

        switch (type) {
            case 'count':
                data = monthlyData.map(m => m.count || 0);
                label = 'Asset Count';
                callback = (value) => value.toLocaleString();
                borderColor = this.colors.trends.count;
                backgroundColor = this.hexToRgba(this.colors.trends.count, 0.1);
                break;
            case 'depreciation':
                data = monthlyData.map(m => m.depreciation || 0);
                label = 'Monthly Depreciation';
                callback = (value) => '₵' + value.toLocaleString('en-GH', {
                    minimumFractionDigits: 0,
                    maximumFractionDigits: 0
                });
                borderColor = this.colors.trends.depreciation;
                backgroundColor = this.hexToRgba(this.colors.trends.depreciation, 0.1);
                break;
            default:
                data = monthlyData.map(m => m.value || 0);
                label = 'Asset Value';
                callback = (value) => '₵' + value.toLocaleString('en-GH', {
                    minimumFractionDigits: 0,
                    maximumFractionDigits: 0
                });
                borderColor = this.colors.trends.value;
                backgroundColor = this.hexToRgba(this.colors.trends.value, 0.1);
        }

        trendChart.data.datasets[0].data = data;
        trendChart.data.datasets[0].label = label;
        trendChart.data.datasets[0].borderColor = borderColor;
        trendChart.data.datasets[0].backgroundColor = backgroundColor;
        trendChart.data.datasets[0].pointBackgroundColor = borderColor;
        trendChart.options.scales.y.ticks.callback = callback;

        trendChart.update();
    }

    setupEventListeners() {
        const refreshBtn = document.getElementById('refreshBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', (e) => {
                e.preventDefault();
                this.refreshDashboard();
            });
        }

        window.exportChart = (chartId) => this.exportChart(chartId);
        window.changeChartView = (type) => this.changeChartView(type);

        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                this.pauseAutoRefresh();
            } else {
                this.resumeAutoRefresh();
            }
        });
    }

    setupLazyLoading() {
        if (!this.config.lazyLoad) return;

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const chartContainer = entry.target;
                    observer.unobserve(chartContainer);
                }
            });
        }, { rootMargin: '50px' });

        document.querySelectorAll('.chart-container').forEach(container => {
            observer.observe(container);
        });
    }

    async refreshDashboard() {
        const refreshBtn = document.getElementById('refreshBtn');
        const originalHtml = refreshBtn?.innerHTML;

        if (refreshBtn) {
            refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Refreshing...';
            refreshBtn.disabled = true;
        }

        try {
            const response = await fetch('?handler=RefreshDashboard');
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const data = await response.json();
            if (data.success) {
                this.showToast('Dashboard refreshed successfully', 'success');
                setTimeout(() => location.reload(), 500);
            } else {
                throw new Error(data.error);
            }
        } catch (error) {
            console.error('Refresh failed:', error);
            this.showToast('Refresh failed: ' + error.message, 'error');
        } finally {
            if (refreshBtn && originalHtml) {
                refreshBtn.innerHTML = originalHtml;
                refreshBtn.disabled = false;
            }
        }
    }

    showToast(message, type = 'info') {
        const toast = document.createElement('div');
        toast.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
        toast.style.cssText = 'top: 20px; right: 20px; z-index: 1050; min-width: 300px;';
        toast.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(toast);
        setTimeout(() => toast.remove(), 4000);
    }

    hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    startAutoRefresh() {
        if (!this.config.autoRefresh) return;
        this.autoRefreshInterval = setInterval(() => {
            this.updateQuickStats();
        }, this.config.refreshInterval);
    }

    pauseAutoRefresh() {
        if (this.autoRefreshInterval) {
            clearInterval(this.autoRefreshInterval);
            this.autoRefreshInterval = null;
        }
    }

    resumeAutoRefresh() {
        if (this.config.autoRefresh && !this.autoRefreshInterval) {
            this.startAutoRefresh();
        }
    }

    destroy() {
        this.pauseAutoRefresh();
        this.charts.forEach(chart => chart.destroy());
        this.charts.clear();
        this.isInitialized = false;
    }
}

// Global functions
function refreshDashboard() {
    if (window.dashboardManager) {
        window.dashboardManager.refreshDashboard();
    }
}

function changeChartView(type) {
    if (window.dashboardManager) {
        window.dashboardManager.changeChartView(type);
    }
}

function exportChart(chartId) {
    if (window.dashboardManager) {
        window.dashboardManager.exportChart(chartId);
    }
}

// Initialize with error handling
document.addEventListener('DOMContentLoaded', function () {
    try {
        window.dashboardManager = new DashboardManager();
        setTimeout(() => {
            window.dashboardManager.initialize();
        }, 100);
    } catch (error) {
        console.error('Failed to create dashboard manager:', error);
    }
});

// Add styles
const styleSheet = document.createElement('style');
styleSheet.textContent = `
    .bi-arrow-clockwise.spin {
        animation: spin 1s linear infinite;
    }
    @keyframes spin {
        from { transform: rotate(0deg); }
        to { transform: rotate(360deg); }
    }
`;
document.head.appendChild(styleSheet);