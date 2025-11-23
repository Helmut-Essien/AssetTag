// Dashboard JavaScript with performance optimizations
class DashboardManager {
    constructor() {
        this.charts = new Map();
        this.isInitialized = false;
        this.autoRefreshInterval = null;
        this.config = {
            autoRefresh: true,
            refreshInterval: 120000, // 2 minutes
            chartAnimation: true,
            lazyLoad: true
        };
    }

    initialize() {
        if (this.isInitialized) return;

        this.initializeCharts();
        this.setupEventListeners();
        this.startAutoRefresh();
        this.setupLazyLoading();

        this.isInitialized = true;
        console.log('Dashboard initialized');
    }

    initializeCharts() {
        this.createStatusChart();
        this.createConditionChart();
        this.createTrendChart();
    }

    createStatusChart() {
        const ctx = document.getElementById('statusChart');
        if (!ctx) return;

        const statusData = window.chartData?.statusData || [];
        const total = statusData.reduce((sum, item) => sum + item.count, 0);

        this.charts.set('status', new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: statusData.map(s => `${s.status} (${((s.count / total) * 100).toFixed(1)}%)`),
                datasets: [{
                    data: statusData.map(s => s.count),
                    backgroundColor: statusData.map(s => window.chartColors.status[s.status] || '#6c757d'),
                    borderWidth: 3,
                    borderColor: '#fff',
                    hoverBorderWidth: 4,
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
                            padding: 20,
                            usePointStyle: true,
                            font: {
                                size: 11
                            }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const percentage = ((value / total) * 100).toFixed(1);
                                return `${label.split(' (')[0]}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                cutout: '70%',
                animation: this.config.chartAnimation ? {
                    animateScale: true,
                    animateRotate: true
                } : false
            }
        }));
    }

    createConditionChart() {
        const ctx = document.getElementById('conditionChart');
        if (!ctx) return;

        const conditionData = window.chartData?.conditionData || [];
        const total = conditionData.reduce((sum, item) => sum + item.count, 0);

        this.charts.set('condition', new Chart(ctx, {
            type: 'pie',
            data: {
                labels: conditionData.map(c => c.condition),
                datasets: [{
                    data: conditionData.map(c => c.count),
                    backgroundColor: conditionData.map(c => window.chartColors.condition[c.condition] || '#6c757d'),
                    borderWidth: 3,
                    borderColor: '#fff',
                    hoverBorderWidth: 4,
                    hoverOffset: 8
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
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = context.raw || 0;
                                const percentage = ((value / total) * 100).toFixed(1);
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                animation: this.config.chartAnimation ? {
                    animateScale: true,
                    animateRotate: true
                } : false
            }
        }));
    }

    createTrendChart() {
        const ctx = document.getElementById('monthlyTrendChart');
        if (!ctx) return;

        const monthlyData = window.chartData?.monthlyData || [];

        this.charts.set('trend', new Chart(ctx, {
            type: 'line',
            data: {
                labels: monthlyData.map(m => m.month),
                datasets: [{
                    label: 'Asset Value',
                    data: monthlyData.map(m => m.value),
                    borderColor: window.chartColors.trends.value,
                    backgroundColor: this.hexToRgba(window.chartColors.trends.value, 0.1),
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4,
                    pointBackgroundColor: window.chartColors.trends.value,
                    pointBorderColor: '#fff',
                    pointBorderWidth: 2,
                    pointRadius: 4,
                    pointHoverRadius: 6
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
                            drawBorder: false
                        },
                        ticks: {
                            callback: function (value) {
                                return '$' + value.toLocaleString();
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
                },
                animation: this.config.chartAnimation ? {
                    duration: 1000,
                    easing: 'easeOutQuart'
                } : false
            }
        }));
    }

    changeChartView(type) {
        const trendChart = this.charts.get('trend');
        if (!trendChart) return;

        const monthlyData = window.chartData?.monthlyData || [];
        const buttonGroup = document.querySelector('.btn-group');
        const buttons = buttonGroup.querySelectorAll('.btn');

        // Update active button
        buttons.forEach(btn => btn.classList.remove('active'));
        event.target.classList.add('active');

        let data, label, callback;

        switch (type) {
            case 'count':
                data = monthlyData.map(m => m.count);
                label = 'Asset Count';
                callback = (value) => value.toLocaleString();
                break;
            case 'depreciation':
                data = monthlyData.map(m => m.depreciation);
                label = 'Monthly Depreciation';
                callback = (value) => '$' + value.toLocaleString();
                break;
            default: // value
                data = monthlyData.map(m => m.value);
                label = 'Asset Value';
                callback = (value) => '$' + value.toLocaleString();
        }

        trendChart.data.datasets[0].data = data;
        trendChart.data.datasets[0].label = label;
        trendChart.options.scales.y.ticks.callback = callback;

        trendChart.update();
    }

    setupEventListeners() {
        // Refresh button
        const refreshBtn = document.getElementById('refreshBtn');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', (e) => {
                e.preventDefault();
                this.refreshDashboard();
            });
        }

        // Export chart functionality
        window.exportChart = (chartId) => this.exportChart(chartId);

        // View change handlers
        window.changeChartView = (type) => this.changeChartView(type);

        // Visibility change for performance
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
                    const chart = entry.target;
                    // Charts are already initialized, but we could add lazy data loading here
                    observer.unobserve(chart);
                }
            });
        }, { rootMargin: '50px' });

        // Observe all chart containers
        document.querySelectorAll('.chart-wrapper canvas').forEach(canvas => {
            observer.observe(canvas);
        });
    }

    async refreshDashboard() {
        const refreshBtn = document.getElementById('refreshBtn');
        const originalHtml = refreshBtn.innerHTML;

        // Show loading state
        refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Refreshing...';
        refreshBtn.disabled = true;

        try {
            const response = await fetch('?handler=RefreshDashboard');
            const data = await response.json();

            if (data.success) {
                // Update last updated time
                document.getElementById('lastUpdated').textContent = data.timestamp;

                // Show success feedback
                this.showToast('Dashboard refreshed successfully', 'success');

                // Reload the page to get fresh data
                setTimeout(() => {
                    window.location.reload();
                }, 500);
            } else {
                throw new Error(data.error);
            }
        } catch (error) {
            console.error('Refresh failed:', error);
            this.showToast('Refresh failed: ' + error.message, 'error');
        } finally {
            // Restore button state
            refreshBtn.innerHTML = originalHtml;
            refreshBtn.disabled = false;
        }
    }

    async updateQuickStats() {
        try {
            const response = await fetch('?handler=QuickStats');
            const data = await response.json();

            if (!data.error) {
                // Update critical stats without full page reload
                this.updateStatsDisplay(data);
            }
        } catch (error) {
            console.error('Failed to update quick stats:', error);
        }
    }

    updateStatsDisplay(data) {
        // This would update specific stat elements without full reload
        // Implementation depends on specific element structure
        console.log('Quick stats updated:', data);
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

    exportChart(chartId) {
        const chart = this.charts.get(chartId);
        if (!chart) return;

        const link = document.createElement('a');
        link.download = `chart-${chartId}-${new Date().toISOString().split('T')[0]}.png`;
        link.href = chart.toBase64Image();
        link.click();
    }

    showToast(message, type = 'info') {
        // Simple toast implementation
        const toast = document.createElement('div');
        toast.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
        toast.style.cssText = 'top: 20px; right: 20px; z-index: 1050; min-width: 300px;';
        toast.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(toast);

        // Auto remove after 3 seconds
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 3000);
    }

    hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    destroy() {
        this.pauseAutoRefresh();
        this.charts.forEach(chart => chart.destroy());
        this.charts.clear();
        this.isInitialized = false;
    }
}

// Global functions for HTML onclick handlers
function refreshDashboard() {
    window.dashboardManager.refreshDashboard();
}

function changeChartView(type) {
    window.dashboardManager.changeChartView(type);
}

function exportChart(chartId) {
    window.dashboardManager.exportChart(chartId);
}

// Initialize dashboard when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    window.dashboardManager = new DashboardManager();
    window.dashboardManager.initialize();
});

// Add spin animation for refresh icon
const style = document.createElement('style');
style.textContent = `
    .bi-arrow-clockwise.spin {
        animation: spin 1s linear infinite;
    }
    @keyframes spin {
        from { transform: rotate(0deg); }
        to { transform: rotate(360deg); }
    }
`;
document.head.appendChild(style);