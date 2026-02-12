const API_BASE_URL = 'http://localhost:5000/api';

let currentFilters = {
    project: '',
    tags: [],
    startDate: '',
    endDate: '',
    status: ''
};

let allTags = [];
let currentResults = [];
let currentTestRuns = [];
let statusChart = null;
let breakdownChart = null;

// Initialize the dashboard
async function init() {
    try {
        await loadProjects();
        await loadTags();
        await loadDashboard();
        
        // Event listeners for filters
        document.getElementById('applyFilters').addEventListener('click', applyFilters);
        document.getElementById('clearFilters').addEventListener('click', clearFilters);
        document.getElementById('refreshBtn').addEventListener('click', refreshData);
        
        // Apply filters on date changes
        document.getElementById('startDate').addEventListener('change', applyFilters);
        document.getElementById('endDate').addEventListener('change', applyFilters);
        document.getElementById('projectFilter').addEventListener('change', applyFilters);
        document.getElementById('statusFilter').addEventListener('change', applyFilters);
    } catch (error) {
        console.error('Initialization error:', error);
        showError('Failed to initialize dashboard');
    }
}

async function loadProjects() {
    try {
        const response = await fetch(`${API_BASE_URL}/projects`);
        if (!response.ok) throw new Error('Failed to load projects');
        
        const projects = await response.json();
        const select = document.getElementById('projectFilter');
        
        projects.forEach(project => {
            const option = document.createElement('option');
            option.value = project;
            option.textContent = project;
            select.appendChild(option);
        });
    } catch (error) {
        console.error('Error loading projects:', error);
    }
}

async function loadTags() {
    try {
        const response = await fetch(`${API_BASE_URL}/tags`);
        if (!response.ok) throw new Error('Failed to load tags');
        
        allTags = await response.json();
        renderTagsFilter();
    } catch (error) {
        console.error('Error loading tags:', error);
    }
}

function renderTagsFilter() {
    const container = document.getElementById('tagsContainer');
    container.innerHTML = '';
    
    allTags.forEach(tag => {
        const label = document.createElement('label');
        label.className = 'tag-checkbox';
        
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.value = tag;
        input.addEventListener('change', (e) => {
            if (e.target.checked) {
                if (!currentFilters.tags.includes(tag)) {
                    currentFilters.tags.push(tag);
                }
            } else {
                currentFilters.tags = currentFilters.tags.filter(t => t !== tag);
            }
        });
        
        label.appendChild(input);
        label.appendChild(document.createTextNode(tag));
        container.appendChild(label);
    });
}

async function loadDashboard() {
    showLoading(true);
    try {
        const params = new URLSearchParams();
        if (currentFilters.project) params.append('project', currentFilters.project);
        if (currentFilters.tags.length > 0) params.append('tags', currentFilters.tags.join(','));
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.status) params.append('status', currentFilters.status);
        
        const response = await fetch(`${API_BASE_URL}/dashboard?${params}`);
        if (!response.ok) throw new Error('Failed to load dashboard data');
        
        const data = await response.json();
        updateStats(data);
        renderTestRuns(data.testRuns);
        updateLastRefresh();
    } catch (error) {
        console.error('Error loading dashboard:', error);
        showError('Failed to load dashboard data');
    } finally {
        showLoading(false);
    }
}

function updateStats(data) {
    document.getElementById('totalTests').textContent = data.totalTests || 0;
    document.getElementById('passedTests').textContent = data.statusCounts?.PASSED || 0;
    document.getElementById('failedTests').textContent = data.statusCounts?.FAILED || 0;
    document.getElementById('skippedTests').textContent = data.statusCounts?.SKIPPED || 0;
    document.getElementById('passRate').textContent = (data.passRate || 0).toFixed(1) + '%';
    
    // Update charts
    updateCharts(data);
}

function renderTestRuns(testRuns) {
    const body = document.getElementById('resultsBody');
    const noResults = document.getElementById('noResults');
    
    currentTestRuns = testRuns || [];
    
    if (!testRuns || testRuns.length === 0) {
        body.innerHTML = '';
        noResults.style.display = 'block';
        return;
    }
    
    noResults.style.display = 'none';
    body.innerHTML = testRuns.map((run, index) => {
        const passRate = run.passRate !== undefined ? run.passRate.toFixed(1) : 0;
        return `
            <tr>
                <td>${escapeHtml(run.name || 'Test Run ' + (index + 1))}</td>
                <td>${formatDate(run.startTime)}</td>
                <td>${(run.results?.length || 0)}</td>
                <td><span class="stat-passed">${run.passedCount || 0}</span></td>
                <td><span class="stat-failed">${run.failedCount || 0}</span></td>
                <td><span class="stat-skipped">${run.skippedCount || 0}</span></td>
                <td>${passRate}%</td>
                <td>
                    <button class="action-btn" onclick="showTestRunModal(${index})">View Tests</button>
                </td>
            </tr>
        `;
    }).join('');
}

function showTestRunModal(runIndex) {
    const testRun = currentTestRuns[runIndex];
    if (!testRun) return;
    
    // Display test run statistics
    const runStatsHtml = `
        <div class="run-stats">
            <div class="stat-mini">
                <strong>Total Tests</strong>
                <span>${testRun.results?.length || 0}</span>
            </div>
            <div class="stat-mini">
                <strong>Passed</strong>
                <span class="passed">${testRun.passedCount || 0}</span>
            </div>
            <div class="stat-mini">
                <strong>Failed</strong>
                <span class="failed">${testRun.failedCount || 0}</span>
            </div>
            <div class="stat-mini">
                <strong>Skipped</strong>
                <span class="skipped">${testRun.skippedCount || 0}</span>
            </div>
            <div class="stat-mini">
                <strong>Pass Rate</strong>
                <span>${(testRun.passRate || 0).toFixed(1)}%</span>
            </div>
        </div>
    `;
    
    // Populate modal header
    document.getElementById('testRunModal').innerHTML = `
        <div class="modal-content">
            <div class="modal-header">
                <h3>${escapeHtml(testRun.name || 'Test Run')}</h3>
                <button class="modal-close" onclick="closeTestRunModal()">&times;</button>
            </div>
            <div class="modal-body">
                ${runStatsHtml}
                <h4>Tests in this Run</h4>
                <table class="tests-in-run-table">
                    <thead>
                        <tr>
                            <th>Test Name</th>
                            <th>Status</th>
                            <th>Duration (ms)</th>
                            <th>Tags</th>
                            <th>Action</th>
                        </tr>
                    </thead>
                    <tbody id="testsInRunBody">
                    </tbody>
                </table>
            </div>
        </div>
    `;
    
    // Populate tests table
    const testsBody = document.getElementById('testsInRunBody');
    if (testRun.results && testRun.results.length > 0) {
        testsBody.innerHTML = testRun.results.map((test, testIndex) => `
            <tr>
                <td>${escapeHtml(test.name)}</td>
                <td>
                    <span class="status-badge ${(test.status || 'PASSED').toLowerCase()}">
                        ${(test.status || 'PASSED').toUpperCase()}
                    </span>
                </td>
                <td>${test.duration || 0}</td>
                <td>
                    ${test.tags && test.tags.length > 0 
                        ? `<div class="tags-list">${test.tags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')}</div>`
                        : '-'
                    }
                </td>
                <td>
                    <button class="action-btn" onclick="showTestDetailsFromRun(${runIndex}, ${testIndex})">View Steps</button>
                </td>
            </tr>
        `).join('');
    } else {
        testsBody.innerHTML = '<tr><td colspan="5">No tests found in this run</td></tr>';
    }
    
    // Show the modal
    document.getElementById('testRunModal').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closeTestRunModal() {
    document.getElementById('testRunModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function showTestDetailsFromRun(runIndex, testIndex) {
    const testRun = currentTestRuns[runIndex];
    if (!testRun || !testRun.results || !testRun.results[testIndex]) return;
    
    const test = testRun.results[testIndex];
    
    // Update main details modal
    document.getElementById('modalTitle').textContent = test.name;
    document.getElementById('modalStatus').innerHTML = `<span class="status-badge ${(test.status || 'PASSED').toLowerCase()}">${(test.status || 'PASSED').toUpperCase()}</span>`;
    document.getElementById('modalProject').textContent = test.project || 'N/A';
    document.getElementById('modalDuration').textContent = (test.duration || 0) + ' ms';
    document.getElementById('modalTimestamp').textContent = formatDate(test.timestamp);
    
    // Render steps
    renderSteps(test.steps || []);
    
    // Render test-level attachments if they exist
    if (test.attachments && test.attachments.length > 0) {
        const stepsContainer = document.getElementById('stepsContainer');
        const attachmentsHtml = `
            <div style="margin-top: 30px; padding-top: 20px; border-top: 2px solid #e5e7eb;">
                <h4 style="color: #1f2937; margin-bottom: 15px; font-size: 16px; font-weight: 600;">Test Attachments</h4>
                <div class="attachment-list">
                    ${test.attachments.map(att => renderAttachment(att)).join('')}
                </div>
            </div>
        `;
        stepsContainer.innerHTML += attachmentsHtml;
    }
    
    // Close test run modal and show details modal
    closeTestRunModal();
    showModal();
}

function renderResults(results) {
    const body = document.getElementById('resultsBody');
    const noResults = document.getElementById('noResults');
    
    currentResults = results || [];
    
    if (!results || results.length === 0) {
        body.innerHTML = '';
        noResults.style.display = 'block';
        return;
    }
    
    noResults.style.display = 'none';
    body.innerHTML = results.map((result, index) => `
        <tr onclick="showTestDetails(${index})" style="cursor: pointer;">
            <td>
                <button class="expand-btn"></button>
            </td>
            <td>
                <span class="status-badge ${result.status}">${result.status}</span>
            </td>
            <td>${escapeHtml(result.name)}</td>
            <td>${escapeHtml(result.project)}</td>
            <td>
                ${result.tags && result.tags.length > 0 
                    ? `<div class="tags-list">${result.tags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')}</div>`
                    : '-'
                }
            </td>
            <td>${result.duration || 0}</td>
            <td>${formatDate(result.timestamp)}</td>
        </tr>
    `).join('');
}

function applyFilters() {
    currentFilters.project = document.getElementById('projectFilter').value;
    currentFilters.status = document.getElementById('statusFilter').value;
    currentFilters.startDate = document.getElementById('startDate').value;
    currentFilters.endDate = document.getElementById('endDate').value;
    
    loadDashboard();
}

function clearFilters() {
    currentFilters = {
        project: '',
        tags: [],
        startDate: '',
        endDate: '',
        status: ''
    };
    
    document.getElementById('projectFilter').value = '';
    document.getElementById('statusFilter').value = '';
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
    
    // Uncheck all tag checkboxes
    document.querySelectorAll('.tag-checkbox input').forEach(checkbox => {
        checkbox.checked = false;
    });
    
    loadDashboard();
}

async function refreshData() {
    try {
        await fetch(`${API_BASE_URL}/refresh`, { method: 'POST' });
        await loadDashboard();
        showSuccess('Data refreshed successfully');
    } catch (error) {
        console.error('Error refreshing data:', error);
        showError('Failed to refresh data');
    }
}

function updateLastRefresh() {
    const now = new Date();
    const timeString = now.toLocaleTimeString('tr-TR');
    document.getElementById('lastUpdate').textContent = `Last updated: ${timeString}`;
}

function showLoading(show) {
    const spinner = document.getElementById('loadingSpinner');
    spinner.style.display = show ? 'block' : 'none';
}

function showError(message) {
    const toast = document.getElementById('errorToast');
    toast.textContent = message;
    toast.style.display = 'block';
    
    setTimeout(() => {
        toast.style.display = 'none';
    }, 5000);
}

function showSuccess(message) {
    const toast = document.getElementById('errorToast');
    toast.textContent = message;
    toast.classList.remove('toast-error');
    toast.classList.add('toast-success');
    toast.style.display = 'block';
    
    setTimeout(() => {
        toast.style.display = 'none';
        toast.classList.add('toast-error');
        toast.classList.remove('toast-success');
    }, 5000);
}

function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('tr-TR') + ' ' + date.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
}

function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function showTestDetails(index) {
    const result = currentResults[index];
    if (!result) return;
    
    // Update modal header and details
    document.getElementById('modalTitle').textContent = result.name;
    document.getElementById('modalStatus').innerHTML = `<span class="status-badge ${result.status}">${result.status}</span>`;
    document.getElementById('modalProject').textContent = result.project;
    document.getElementById('modalDuration').textContent = result.duration;
    document.getElementById('modalTimestamp').textContent = formatDate(result.timestamp);
    
    // Fetch full test details with steps
    fetchTestDetails(result.source).then(fullTest => {
        renderSteps(fullTest.steps || []);
        showModal();
    }).catch(error => {
        console.error('Error fetching test details:', error);
        renderSteps([]);
        showModal();
    });
}

async function fetchTestDetails(filePath) {
    try {
        // For now, we'll use the cached results which should have steps
        // In a real scenario, you might fetch from the API endpoint
        return Promise.resolve(currentResults[currentResults.findIndex(r => r.source === filePath)] || {});
    } catch (error) {
        console.error('Error fetching test details:', error);
        return {};
    }
}

function renderSteps(steps) {
    const container = document.getElementById('stepsContainer');
    
    if (!steps || steps.length === 0) {
        container.innerHTML = '<div class="no-steps">No steps recorded</div>';
        return;
    }
    
    container.innerHTML = steps.map(step => `
        <div class="step">
            <div class="step-name">${escapeHtml(step.name || 'Unknown Step')}</div>
            <div class="step-status">
                <span class="step-status-badge ${(step.status || 'passed').toLowerCase()}">
                    ${(step.status || 'passed').toUpperCase()}
                </span>
                ${step.stop && step.start ? `<span class="step-duration">${step.stop - step.start}ms</span>` : ''}
            </div>
            ${step.attachments && step.attachments.length > 0 ? `
                <div class="attachments-section">
                    <h5 style="margin: 10px 0 8px 0; font-size: 12px; color: #666;">Attachments</h5>
                    <div class="attachment-list">
                        ${step.attachments.map(att => renderAttachment(att)).join('')}
                    </div>
                </div>
            ` : ''}
        </div>
    `).join('');
}

function renderAttachment(attachment) {
    const isImage = attachment.type && (attachment.type.startsWith('image/') || /\.(png|jpg|jpeg|gif|webp)$/i.test(attachment.source));
    const fileName = attachment.name || attachment.source?.split('/').pop() || 'Attachment';
    
    return `
        <div class="attachment-item">
            <div class="attachment-thumbnail">
                ${isImage ? `<img src="${escapeHtml(attachment.source)}" alt="${escapeHtml(fileName)}">` : '<div style="color: #999; text-align: center;">📎</div>'}
            </div>
            <div class="attachment-info">
                <div class="attachment-name">${escapeHtml(fileName)}</div>
                <a href="${escapeHtml(attachment.source)}" download class="attachment-link">Download</a>
            </div>
        </div>
    `;
}

function showModal() {
    document.getElementById('detailsModal').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closeDetailsModal() {
    document.getElementById('detailsModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function updateCharts(data) {
    const statusCounts = data.statusCounts || {
        PASSED: 0,
        FAILED: 0,
        SKIPPED: 0,
        BROKEN: 0
    };

    updateStatusChart(statusCounts);
    updateBreakdownChart(statusCounts);
}

function updateStatusChart(statusCounts) {
    const ctx = document.getElementById('statusChart');
    if (!ctx) return;

    const colors = ['#059669', '#dc2626', '#d97706', '#d97706'];
    const labels = ['Passed', 'Failed', 'Skipped', 'Broken'];
    const data = [
        statusCounts.PASSED || 0,
        statusCounts.FAILED || 0,
        statusCounts.SKIPPED || 0,
        statusCounts.BROKEN || 0
    ];

    if (statusChart) {
        statusChart.destroy();
    }

    statusChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: colors,
                borderColor: '#fff',
                borderWidth: 2,
                hoverOffset: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: {
                        font: { size: 12, weight: '600' },
                        padding: 15,
                        color: '#1f2937'
                    }
                }
            }
        }
    });
}

function updateBreakdownChart(statusCounts) {
    const ctx = document.getElementById('breakdownChart');
    if (!ctx) return;

    const colors = ['#059669', '#dc2626', '#d97706', '#d97706'];
    const labels = ['Passed', 'Failed', 'Skipped', 'Broken'];
    const data = [
        statusCounts.PASSED || 0,
        statusCounts.FAILED || 0,
        statusCounts.SKIPPED || 0,
        statusCounts.BROKEN || 0
    ];

    if (breakdownChart) {
        breakdownChart.destroy();
    }

    breakdownChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Test Count',
                data: data,
                backgroundColor: colors,
                borderColor: colors,
                borderWidth: 0,
                borderRadius: 6,
                hoverBackgroundColor: ['#059669', '#dc2626', '#d97706', '#d97706']
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            indexAxis: 'y',
            plugins: {
                legend: {
                    display: true,
                    labels: {
                        font: { size: 12, weight: '600' },
                        padding: 15,
                        color: '#1f2937'
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1,
                        color: '#6b7280',
                        font: { size: 12 }
                    },
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
                    }
                },
                y: {
                    ticks: {
                        color: '#6b7280',
                        font: { size: 12, weight: '600' }
                    },
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
}

// Close modal on background click and initialize on page load
document.addEventListener('DOMContentLoaded', function() {
    document.getElementById('detailsModal').addEventListener('click', function(e) {
        if (e.target === this) {
            closeDetailsModal();
        }
    });
    
    document.getElementById('testRunModal').addEventListener('click', function(e) {
        if (e.target === this) {
            closeTestRunModal();
        }
    });
    
    init();
});
