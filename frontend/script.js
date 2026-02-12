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
let timeGroupedTestCases = [];
let statusChart = null;
let breakdownChart = null;
let currentTimeGroupView = 'card'; // 'card' or 'table'

// Initialize the dashboard
async function init() {
    try {
        await loadProjects();
        await loadTags();
        await loadDashboard();
        await loadTestCasesByTime();

        // Set default date values: startDate = 1 month before today, endDate = today
        const today = new Date();
        const yyyy = today.getFullYear();
        const mm = String(today.getMonth() + 1).padStart(2, '0');
        const dd = String(today.getDate()).padStart(2, '0');
        const endDateStr = `${yyyy}-${mm}-${dd}`;
        // Calculate 1 month before
        const oneMonthAgo = new Date(today);
        oneMonthAgo.setMonth(today.getMonth() - 1);
        const prevYyyy = oneMonthAgo.getFullYear();
        const prevMm = String(oneMonthAgo.getMonth() + 1).padStart(2, '0');
        const prevDd = String(oneMonthAgo.getDate()).padStart(2, '0');
        const startDateStr = `${prevYyyy}-${prevMm}-${prevDd}`;
        const startDateInput = document.getElementById('startDate');
        const endDateInput = document.getElementById('endDate');
        if (startDateInput && !startDateInput.value) startDateInput.value = startDateStr;
        if (endDateInput && !endDateInput.value) endDateInput.value = endDateStr;

        // Event listeners for filters
        document.getElementById('applyFilters').addEventListener('click', applyFilters);
        document.getElementById('clearFilters').addEventListener('click', clearFilters);
        document.getElementById('refreshBtn').addEventListener('click', refreshData);

        // Apply filters on date changes
        document.getElementById('startDate').addEventListener('change', applyFilters);
        document.getElementById('endDate').addEventListener('change', applyFilters);
        document.getElementById('projectFilter').addEventListener('change', applyFilters);
        document.getElementById('statusFilter').addEventListener('change', applyFilters);

        // Event listeners for time group view toggle
        document.getElementById('timeGroupCardView').addEventListener('click', function() {
            currentTimeGroupView = 'card';
            document.getElementById('timeGroupCardView').classList.add('active');
            document.getElementById('timeGroupTableView').classList.remove('active');
            renderTimeGroupedTestCases();
        });

        document.getElementById('timeGroupTableView').addEventListener('click', function() {
            currentTimeGroupView = 'table';
            document.getElementById('timeGroupTableView').classList.add('active');
            document.getElementById('timeGroupCardView').classList.remove('active');
            renderTimeGroupedTestCases();
        });

        // Keyboard event listener for closing lightbox with Escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                const lightbox = document.getElementById('imageLightbox');
                if (lightbox && lightbox.style.display !== 'none') {
                    closeImageLightbox();
                }
            }
        });
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
    const dropdown = document.getElementById('tagsDropdown');
    const searchInput = document.getElementById('tagsSearch');
    const selectedTagsDiv = document.getElementById('selectedTags');
    dropdown.innerHTML = '';
    let filteredTags = allTags;

    function renderDropdown() {
        dropdown.innerHTML = '';
        filteredTags.forEach(tag => {
            const label = document.createElement('label');
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.value = tag;
            checkbox.checked = currentFilters.tags.includes(tag);
            checkbox.addEventListener('change', function() {
                if (this.checked) {
                    if (!currentFilters.tags.includes(tag)) currentFilters.tags.push(tag);
                } else {
                    currentFilters.tags = currentFilters.tags.filter(t => t !== tag);
                }
                renderDropdown();
                renderSelectedTags();
                applyFilters();
            });
            label.appendChild(checkbox);
            label.appendChild(document.createTextNode(tag));
            dropdown.appendChild(label);
        });
    }

    function renderSelectedTags() {
        selectedTagsDiv.innerHTML = '';
        currentFilters.tags.forEach(tag => {
            const badge = document.createElement('span');
            badge.className = 'tag-badge';
            badge.textContent = tag;
            const removeBtn = document.createElement('span');
            removeBtn.className = 'remove-tag';
            removeBtn.textContent = '×';
            removeBtn.onclick = function() {
                currentFilters.tags = currentFilters.tags.filter(t => t !== tag);
                renderDropdown();
                renderSelectedTags();
                applyFilters();
            };
            badge.appendChild(removeBtn);
            selectedTagsDiv.appendChild(badge);
        });
    }

    searchInput.oninput = function() {
        const val = searchInput.value.toLowerCase();
        filteredTags = allTags.filter(tag => tag.toLowerCase().includes(val));
        renderDropdown();
    };

    searchInput.onfocus = function() {
        dropdown.classList.add('open');
    };
    searchInput.onblur = function() {
        setTimeout(() => dropdown.classList.remove('open'), 150);
    };

    renderDropdown();
    renderSelectedTags();
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


function applyFilters() {
    currentFilters.project = document.getElementById('projectFilter').value;
    currentFilters.status = document.getElementById('statusFilter').value;
    currentFilters.startDate = document.getElementById('startDate').value;
    currentFilters.endDate = document.getElementById('endDate').value;
    
    loadDashboard();
    loadTestCasesByTime();
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
        await loadTestCasesByTime();
        showSuccess('Data refreshed successfully');
    } catch (error) {
        console.error('Error refreshing data:', error);
        showError('Failed to refresh data');
    }
}

async function loadTestCasesByTime() {
    const container = document.getElementById('groupedByTimeContainer');
    if (!container) return;
    
    container.innerHTML = '<p>Loading test cases by time...</p>';

    try {
        const params = new URLSearchParams();
        if (currentFilters.project) params.append('project', currentFilters.project);
        if (currentFilters.tags.length > 0) params.append('tags', currentFilters.tags.join(','));
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.status) params.append('status', currentFilters.status);
        
        const response = await fetch(`${API_BASE_URL}/test-cases-by-time?${params}`);
        if (!response.ok) throw new Error('Failed to load test cases by time');

        timeGroupedTestCases = await response.json();
        if (!timeGroupedTestCases || timeGroupedTestCases.length === 0) {
            container.innerHTML = '<p>No test cases found.</p>';
            return;
        }

        renderTimeGroupedTestCases();
    } catch (error) {
        console.error('Error loading test cases by time:', error);
        container.innerHTML = '<p>Failed to load test cases by time.</p>';
    }
}

function renderTimeGroupedTestCases() {
    const container = document.getElementById('groupedByTimeContainer');
    if (!container || !timeGroupedTestCases) return;

    if (currentTimeGroupView === 'table') {
        renderTimeGroupedTable();
    } else {
        renderTimeGroupedCards();
    }
}

function renderTimeGroupedCards() {
    const container = document.getElementById('groupedByTimeContainer');
    container.innerHTML = timeGroupedTestCases.map((group, index) => {
        const passRateColor = group.passRate >= 80 ? '#059669' : group.passRate >= 50 ? '#d97706' : '#dc2626';
        // Extract unique tags from all test cases in this group
        const uniqueTags = [...new Set(group.testCases.flatMap(test => test.tags || []))].sort();
        const tagsHtml = uniqueTags.length > 0 ? `<div class="time-group-tags">${uniqueTags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')}</div>` : '';
        return `
            <div class="time-group-card" onclick="showTimeGroupDetails(${index})" style="cursor: pointer;">
                <div class="time-group-header">
                    <div>
                        <h3>${group.timeGroup}</h3>
                        ${tagsHtml}
                    </div>
                    <span class="pass-rate" style="color: ${passRateColor};">${group.passRate.toFixed(1)}%</span>
                </div>
                <div class="time-group-stats">
                    <span class="stat-badge passed">${group.passedCount} Passed</span>
                    <span class="stat-badge failed">${group.failedCount} Failed</span>
                    <span class="stat-badge skipped">${group.skippedCount} Skipped</span>
                    <span class="stat-badge broken">${group.brokenCount} Broken</span>
                </div>
                <div class="test-cases-list">
                    <strong>Test Cases (${group.testCases.length}):</strong>
                    <ul>
                        ${group.testCases.slice(0, 5).map(test => `
                            <li>
                                <span class="status-badge ${test.status.toLowerCase()}">${test.status}</span>
                                <span>${escapeHtml(test.name)}</span>
                            </li>
                        `).join('')}
                        ${group.testCases.length > 5 ? `<li style="font-style: italic; color: #9ca3af;">... and ${group.testCases.length - 5} more</li>` : ''}
                    </ul>
                </div>
                <div style="margin-top: 12px; text-align: center; color: #667eea; font-size: 12px; font-weight: 600;">Click to view all test cases</div>
            </div>
        `;
    }).join('');
}

function renderTimeGroupedTable() {
    const container = document.getElementById('groupedByTimeContainer');
    container.innerHTML = `
        <table class="time-groups-table">
            <thead>
                <tr>
                    <th>Time Group</th>
                    <th>Pass Rate</th>
                    <th>Total</th>
                    <th>Passed</th>
                    <th>Failed</th>
                    <th>Skipped</th>
                    <th>Broken</th>
                    <th>Action</th>
                </tr>
            </thead>
            <tbody>
                ${timeGroupedTestCases.map((group, index) => {
                    const hasFailed = group.failedCount > 0;
                    const rowClass = hasFailed ? 'failed-row' : '';
                    const passRateColor = group.passRate >= 80 ? '#059669' : group.passRate >= 50 ? '#d97706' : '#dc2626';
                    return `
                        <tr class="${rowClass}">
                            <td><strong>${escapeHtml(group.timeGroup)}</strong></td>
                            <td>
                                <span class="pass-rate" style="color: ${passRateColor};">
                                    ${group.passRate.toFixed(1)}%
                                </span>
                            </td>
                            <td>${group.testCases.length}</td>
                            <td><span class="stat-badge passed">${group.passedCount}</span></td>
                            <td><span class="stat-badge failed">${group.failedCount}</span></td>
                            <td><span class="stat-badge skipped">${group.skippedCount}</span></td>
                            <td><span class="stat-badge broken">${group.brokenCount}</span></td>
                            <td>
                                <button class="action-btn" onclick="showTimeGroupDetails(${index})" style="font-size: 12px; padding: 4px 8px;">View</button>
                            </td>
                        </tr>
                    `;
                }).join('')}
            </tbody>
        </table>
    `;
}

function showTimeGroupDetails(groupIndex) {
    const group = timeGroupedTestCases[groupIndex];
    if (!group) return;

    // Update modal header
    document.getElementById('timeGroupModalTitle').textContent = `Test Cases - ${group.timeGroup}`;
    
    // Update statistics
    document.getElementById('groupTotalTests').textContent = group.testCases.length;
    document.getElementById('groupPassedCount').textContent = group.passedCount;
    document.getElementById('groupFailedCount').textContent = group.failedCount;
    document.getElementById('groupSkippedCount').textContent = group.skippedCount;
    document.getElementById('groupBrokenCount').textContent = group.brokenCount;
    document.getElementById('groupPassRate').textContent = group.passRate.toFixed(1) + '%';

    // Populate test cases table
    const testsBody = document.getElementById('testsInGroupBody');
    testsBody.innerHTML = group.testCases.map((test, testIndex) => `
        <tr>
            <td>${escapeHtml(test.name)}</td>
            <td>
                <span class="status-badge ${test.status.toLowerCase()}">
                    ${test.status}
                </span>
            </td>
            <td>${test.duration || 0}</td>
            <td>${escapeHtml(test.project || '-')}</td>
            <td>
                ${test.tags && test.tags.length > 0
                    ? `<div class="tags-list">${test.tags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')}</div>`
                    : '-'
                }
            </td>
            <td>
                <button class="action-btn" onclick="showTimeGroupTestDetails(${groupIndex}, ${testIndex})" style="font-size: 12px; padding: 4px 8px;">View</button>
            </td>
        </tr>
    `).join('');

    // Show the modal
    document.getElementById('timeGroupModal').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closeTimeGroupModal() {
    document.getElementById('timeGroupModal').style.display = 'none';
    document.body.style.overflow = 'auto';
}

function showTimeGroupTestDetails(groupIndex, testIndex) {
    const group = timeGroupedTestCases[groupIndex];
    if (!group || !group.testCases || !group.testCases[testIndex]) return;

    const test = group.testCases[testIndex];

    // Update details modal
    document.getElementById('modalTitle').textContent = test.name;
    document.getElementById('modalStatus').innerHTML = `<span class="status-badge ${test.status.toLowerCase()}">${test.status}</span>`;
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

    // Close time group modal and show details modal
    closeTimeGroupModal();
    showModal();
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
    
    // The attachment.source should already be in the format "data/test-results/filename"
    const attachmentUrl = `${API_BASE_URL}/attachment/${attachment.source}`;
    
    return `
        <div class="attachment-item">
            <div class="attachment-thumbnail" ${isImage ? `onclick="openImageLightbox('${escapeHtml(attachmentUrl)}', '${escapeHtml(fileName)}')"` : ''} ${isImage ? 'style="cursor: pointer;"' : ''}>
                ${isImage ? `<img src="${escapeHtml(attachmentUrl)}" alt="${escapeHtml(fileName)}" style="cursor: pointer;" onerror="this.src='data:image/svg+xml,%3Csvg xmlns=%22http://www.w3.org/2000/svg%22 width=%22100%22 height=%22100%22%3E%3Crect fill=%22%23f0f0f0%22 width=%22100%22 height=%22100%22/%3E%3Ctext x=%2250%25%22 y=%2250%25%22 text-anchor=%22middle%22 dy=%22.3em%22 font-family=%22sans-serif%22 font-size=%2212%22 fill=%22%23999%22%3EImage Failed%3C/text%3E%3C/svg%3E'">` : '<div style="color: #999; text-align: center;">📎</div>'}
            </div>
            <div class="attachment-info">
                <div class="attachment-name">${escapeHtml(fileName)}</div>
                <a href="${escapeHtml(attachmentUrl)}" download class="attachment-link">Download</a>
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

function openImageLightbox(imageUrl, fileName) {
    document.getElementById('lightboxImage').src = imageUrl;
    document.getElementById('lightboxFileName').textContent = fileName;
    document.getElementById('imageLightbox').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function closeImageLightbox() {
    document.getElementById('imageLightbox').style.display = 'none';
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

    document.getElementById('timeGroupModal').addEventListener('click', function(e) {
        if (e.target === this) {
            closeTimeGroupModal();
        }
    });
    
    init();
});


