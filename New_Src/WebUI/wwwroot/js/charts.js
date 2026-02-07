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
            const optionLabels = config.options && Array.isArray(config.options.xLabels) ? config.options.xLabels : null;
            const dataLabels = config.data && Array.isArray(config.data.labels) ? config.data.labels : null;
            const labels = optionLabels || dataLabels;

            if (config.type === 'scatter' && config.options && Array.isArray(labels) && labels.length > 0) {
                config.options.scales = config.options.scales || {};
                config.options.scales.x = config.options.scales.x || {};
                config.options.scales.x.ticks = config.options.scales.x.ticks || {};
                config.options.scales.x.ticks.callback = function(value) {
                    if (labels.length === 1) {
                        return Math.abs(value) <= 0.51 ? labels[0] : '';
                    }
                    const idx = Math.round(value);
                    return (idx >= 0 && idx < labels.length) ? labels[idx] : '';
                };
                config.options.scales.x.type = 'linear';
                config.options.scales.x.display = true;
                config.options.scales.x.ticks.autoSkip = false;
                // Keep explicit bounds only when we have more than one label.
                if (labels.length > 1) {
                    config.options.scales.x.min = -0.5;
                    config.options.scales.x.max = labels.length - 0.5;
                    config.options.scales.x.ticks.stepSize = 1;
                } else {
                    // For single label, keep a symmetric window to guarantee visible x-axis tick text.
                    config.options.scales.x.min = -0.5;
                    config.options.scales.x.max = 0.5;
                    config.options.scales.x.ticks.stepSize = 1;
                }
            }
        } catch (e) {
            console.warn('[createChart] Failed to attach xLabels callback', e);
        }

        // Keep chart axes stable: accidental mouse wheel over chart should not zoom/pan.
        config.options = config.options || {};
        config.options.plugins = config.options.plugins || {};
        config.options.plugins.zoom = {
            pan: { enabled: false },
            zoom: {
                wheel: { enabled: false },
                pinch: { enabled: false },
                drag: { enabled: false }
            }
        };

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

