// Chart.js Helper Functions

const chartInstances = {};

/**
 * Creates or updates a chart using Chart.js
 * @param {string} canvasId - The ID of the canvas element
 * @param {object} config - Chart.js configuration object
 */
function createChart(canvasId, config) {
    try {
        console.log(`[createChart] Attempting to create chart: ${canvasId}`);
        
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error(`[createChart] Canvas element with ID "${canvasId}" not found`);
            return;
        }

        if (chartInstances[canvasId]) {
            chartInstances[canvasId].destroy();
        }

        const ctx = canvas.getContext('2d');

        if (!config.type) return;

        // --- Handle Scatter xLabels callback (کد قبلی شما) ---
        try {
            if (config.type === 'scatter' && config.options && Array.isArray(config.options.xLabels)) {
                config.options.scales = config.options.scales || {};
                config.options.scales.x = config.options.scales.x || {};
                config.options.scales.x.ticks = config.options.scales.x.ticks || {};
                const labels = config.options.xLabels;
                config.options.scales.x.ticks.callback = function(value) {
                    const idx = Math.round(value);
                    return labels[idx] ?? '';
                };
                config.options.scales.x.type = 'linear';
                config.options.scales.x.min = -0.5;
                config.options.scales.x.max = labels.length - 0.5;
                config.options.scales.x.ticks.stepSize = 1;
            }
        } catch (e) {
            console.warn('[createChart] Failed to attach xLabels callback', e);
        }

        // --- بخش جدید: اضافه کردن تنظیمات Zoom ---
        config.options = config.options || {};
        config.options.plugins = config.options.plugins || {};

        // تنظیمات استاندارد زوم و پن
        config.options.plugins.zoom = {
            pan: {
                enabled: true,
                mode: 'xy', // اجازه جابجایی در هر دو جهت
                modifierKey: null, // برای پن کردن نیاز به کلید خاصی نیست (یا مثلا 'ctrl')
            },
            zoom: {
                wheel: {
                    enabled: true, // فعال کردن زوم با اسکرول موس
                    speed: 0.1,    // سرعت زوم
                },
                pinch: {
                    enabled: true  // فعال کردن زوم با دو انگشت (تاچ)
                },
                mode: 'xy', // زوم در هر دو جهت (X و Y)
                drag: {
                    enabled: false, // اگر true باشد، با کشیدن موس کادر زوم ایجاد می‌شود
                }
            },
            limits: {
                y: { min: 'original', max: 'original' }, // جلوگیری از زوم‌اوت بیش از حد (اختیاری)
                // x: {min: 'original', max: 'original'},
            }
        };

        // یک دکمه ریست هم اضافه میکنیم که با دوبار کلیک زوم ریست شود (اختیاری ولی کاربردی)
        // برای این کار باید ایونت هندلر جدا بنویسیم، اما فعلا تنظیمات زوم کافیست.

        chartInstances[canvasId] = new Chart(ctx, config);
        console.log(`✓ Chart "${canvasId}" created successfully`);
    } catch (error) {
        console.error(`✗ Error creating chart "${canvasId}":`, error);
    }
}

/**
 * Destroys a chart by ID
 * @param {string} canvasId - The ID of the canvas element
 */
function destroyChart(canvasId) {
    if (chartInstances[canvasId]) {
        chartInstances[canvasId].destroy();
        delete chartInstances[canvasId];
        console.log(`[destroyChart] Chart "${canvasId}" destroyed`);
    }
}

/**
 * Updates chart data without recreating
 * @param {string} canvasId - The ID of the canvas element
 * @param {object} newData - New data object with labels and datasets
 */
function updateChartData(canvasId, newData) {
    try {
        if (chartInstances[canvasId]) {
            chartInstances[canvasId].data.labels = newData.labels;
            chartInstances[canvasId].data.datasets = newData.datasets;
            chartInstances[canvasId].update();
            console.log(`[updateChartData] Chart "${canvasId}" updated`);
        } else {
            console.warn(`[updateChartData] Chart "${canvasId}" not found. Cannot update.`);
        }
    } catch (error) {
        console.error(`[updateChartData] Error updating chart "${canvasId}":`, error);
    }
}

/**
 * Resizes all charts to fit their containers
 */
function resizeAllCharts() {
    try {
        let resized = 0;
        Object.keys(chartInstances).forEach(key => {
            const chart = chartInstances[key];
            if (chart) {
                chart.resize();
                resized++;
            }
        });
        console.log(`[resizeAllCharts] Resized ${resized} charts`);
    } catch (error) {
        console.error('[resizeAllCharts] Error resizing charts:', error);
    }
}

// Handle window resize events
window.addEventListener('resize', () => {
    resizeAllCharts();
});

// Log that the script loaded
console.log('✓ charts.js loaded successfully');
console.log(`  Chart.js version: ${typeof Chart !== 'undefined' ? 'Loaded' : 'Not loaded'}`);

