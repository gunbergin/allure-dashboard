const API_BASE_URL = 'http://localhost:5000/api';

let currentFilters = {
    projects: [],
    tags: [],
    statuses: [],
    startDate: '',
    endDate: ''
};

let allTags = [];
let allProjects = [];
const allStatuses = ['PASSED', 'FAILED', 'SKIPPED', 'BROKEN'];
let currentResults = [];
let currentTestRuns = [];
let timeGroupedTestCases = [];
let statusChart = null;
let breakdownChart = null;
let currentTimeGroupView = 'card'; // 'card' or 'table'

// Initialize the dashboard
async function init() {
    try {
        // Set default date values FIRST: startDate = 1 week (7 days) before today, endDate = tomorrow (today+1)
        // endDate is set to tomorrow so the backend's <= filter includes all of today's results
        const today = new Date();
        const tomorrow = new Date(today);
        tomorrow.setDate(today.getDate() + 1);
        const endYyyy = tomorrow.getFullYear();
        const endMm = String(tomorrow.getMonth() + 1).padStart(2, '0');
        const endDd = String(tomorrow.getDate()).padStart(2, '0');
        const endDateStr = `${endYyyy}-${endMm}-${endDd}`;
        // Display today's date in the date picker for the user
        const yyyy = today.getFullYear();
        const mm = String(today.getMonth() + 1).padStart(2, '0');
        const dd = String(today.getDate()).padStart(2, '0');
        const endDateDisplayStr = `${yyyy}-${mm}-${dd}`;
        // Calculate 7 days ago
        const sevenDaysAgo = new Date(today);
        sevenDaysAgo.setDate(today.getDate() - 7);
        const prevYyyy = sevenDaysAgo.getFullYear();
        const prevMm = String(sevenDaysAgo.getMonth() + 1).padStart(2, '0');
        const prevDd = String(sevenDaysAgo.getDate()).padStart(2, '0');
        const startDateStr = `${prevYyyy}-${prevMm}-${prevDd}`;
        const startDateInput = document.getElementById('startDate');
        const endDateInput = document.getElementById('endDate');
        if (startDateInput && !startDateInput.value) startDateInput.value = startDateStr;
        if (endDateInput && !endDateInput.value) endDateInput.value = endDateDisplayStr;
        // Apply initial filters to load with 1-week default
        currentFilters.startDate = startDateStr;
        currentFilters.endDate = endDateStr;

        await loadProjects();
        await loadTags();
        await loadDashboard();
        await loadTestCasesByTime();

        // Event listeners for filters
        document.getElementById('applyFilters').addEventListener('click', applyFilters);
        document.getElementById('clearFilters').addEventListener('click', clearFilters);
        document.getElementById('refreshBtn').addEventListener('click', refreshData);
        document.getElementById('exportBtn').addEventListener('click', generateExcelReport);

        // Projects, tags, and statuses filters use their own event listeners in their respective render functions
        // document.getElementById('statusFilter').addEventListener('change', applyFilters);

        // Event listeners for time group view toggle
        document.getElementById('timeGroupCardView').addEventListener('click', function () {
            currentTimeGroupView = 'card';
            document.getElementById('timeGroupCardView').classList.add('active');
            document.getElementById('timeGroupTableView').classList.remove('active');
            renderTimeGroupedTestCases();
        });

        document.getElementById('timeGroupTableView').addEventListener('click', function () {
            currentTimeGroupView = 'table';
            document.getElementById('timeGroupTableView').classList.add('active');
            document.getElementById('timeGroupCardView').classList.remove('active');
            renderTimeGroupedTestCases();
        });

        // Keyboard event listener for closing lightbox with Escape key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                const lightbox = document.getElementById('imageLightbox');
                if (lightbox && lightbox.style.display !== 'none') {
                    closeImageLightbox();
                }
            }
        });

        // Update last updated timestamp
        updateLastUpdatedTime();
    } catch (error) {
        console.error('Initialization error:', error);
        showError('Failed to initialize dashboard');
    }
}

async function loadProjects() {
    try {
        const response = await fetch(`${API_BASE_URL}/projects`);
        if (!response.ok) throw new Error('Failed to load projects');

        allProjects = await response.json();
        renderProjectsFilter();
    } catch (error) {
        console.error('Error loading projects:', error);
    }
}

function renderProjectsFilter() {
    const dropdown = document.getElementById('projectsDropdown');
    const searchInput = document.getElementById('projectsSearch');
    const selectedProjectsDiv = document.getElementById('selectedProjects');
    let filteredProjects = allProjects;

    function renderDropdown() {
        dropdown.innerHTML = '';
        filteredProjects.forEach(project => {
            const label = document.createElement('label');
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'project-checkbox';
            checkbox.value = project;
            checkbox.checked = currentFilters.projects.includes(project);
            checkbox.addEventListener('change', function () {
                if (this.checked) {
                    if (!currentFilters.projects.includes(project)) currentFilters.projects.push(project);
                } else {
                    currentFilters.projects = currentFilters.projects.filter(p => p !== project);
                }
                renderDropdown();
                renderSelectedProjects();
                applyFilters();
            });
            label.appendChild(checkbox);
            label.appendChild(document.createTextNode(project));
            dropdown.appendChild(label);
        });
    }

    function renderSelectedProjects() {
        selectedProjectsDiv.innerHTML = '';
        currentFilters.projects.forEach(project => {
            const badge = document.createElement('span');
            badge.className = 'project-badge';
            badge.textContent = project.split('/').pop(); // Show only last part
            badge.title = project; // Full path on hover
            const removeBtn = document.createElement('span');
            removeBtn.className = 'remove-project';
            removeBtn.textContent = '×';
            removeBtn.onclick = function (e) {
                e.stopPropagation();
                currentFilters.projects = currentFilters.projects.filter(p => p !== project);
                renderDropdown();
                renderSelectedProjects();
                applyFilters();
            };
            badge.appendChild(removeBtn);
            selectedProjectsDiv.appendChild(badge);
        });
    }

    searchInput.oninput = function () {
        const val = searchInput.value.toLowerCase();
        filteredProjects = allProjects.filter(p => p.toLowerCase().includes(val));
        renderDropdown();
    };

    searchInput.onfocus = function () {
        dropdown.classList.add('open');
    };
    searchInput.onblur = function () {
        setTimeout(() => dropdown.classList.remove('open'), 150);
    };

    renderDropdown();
    renderSelectedProjects();
}

async function loadTags() {
    try {
        const response = await fetch(`${API_BASE_URL}/tags`);
        if (!response.ok) throw new Error('Failed to load tags');

        allTags = await response.json();
        renderTagsFilter();
        renderStatusesFilter(); // Initial render for statuses as well
    } catch (error) {
        console.error('Error loading tags:', error);
    }
}

function renderStatusesFilter() {
    const dropdown = document.getElementById('statusDropdown');
    const searchInput = document.getElementById('statusSearch');
    const selectedStatusesDiv = document.getElementById('selectedStatuses');
    let filteredStatuses = allStatuses;

    function renderDropdown() {
        dropdown.innerHTML = '';
        filteredStatuses.forEach(status => {
            const label = document.createElement('label');
            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.className = 'status-checkbox';
            checkbox.value = status;
            checkbox.checked = currentFilters.statuses.includes(status);
            checkbox.addEventListener('change', function () {
                if (this.checked) {
                    if (!currentFilters.statuses.includes(status)) currentFilters.statuses.push(status);
                } else {
                    currentFilters.statuses = currentFilters.statuses.filter(s => s !== status);
                }
                renderDropdown();
                renderSelectedStatuses();
                applyFilters();
            });
            label.appendChild(checkbox);
            label.appendChild(document.createTextNode(status.charAt(0) + status.slice(1).toLowerCase()));
            dropdown.appendChild(label);
        });
    }

    function renderSelectedStatuses() {
        selectedStatusesDiv.innerHTML = '';
        currentFilters.statuses.forEach(status => {
            const badge = document.createElement('span');
            badge.className = 'status-badge';
            badge.textContent = status.charAt(0) + status.slice(1).toLowerCase();
            const removeBtn = document.createElement('span');
            removeBtn.className = 'remove-status';
            removeBtn.textContent = '×';
            removeBtn.onclick = function (e) {
                e.stopPropagation();
                currentFilters.statuses = currentFilters.statuses.filter(s => s !== status);
                renderDropdown();
                renderSelectedStatuses();
                applyFilters();
            };
            badge.appendChild(removeBtn);
            selectedStatusesDiv.appendChild(badge);
        });
    }

    searchInput.oninput = function () {
        const val = searchInput.value.toLowerCase();
        filteredStatuses = allStatuses.filter(s => s.toLowerCase().includes(val));
        renderDropdown();
    };

    searchInput.onfocus = function () {
        dropdown.classList.add('open');
    };
    searchInput.onblur = function () {
        setTimeout(() => dropdown.classList.remove('open'), 150);
    };

    renderDropdown();
    renderSelectedStatuses();
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
            checkbox.className = 'tag-checkbox';
            checkbox.value = tag;
            checkbox.checked = currentFilters.tags.includes(tag);
            checkbox.addEventListener('change', function () {
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
            removeBtn.onclick = function () {
                currentFilters.tags = currentFilters.tags.filter(t => t !== tag);
                renderDropdown();
                renderSelectedTags();
                applyFilters();
            };
            badge.appendChild(removeBtn);
            selectedTagsDiv.appendChild(badge);
        });
    }

    searchInput.oninput = function () {
        const val = searchInput.value.toLowerCase();
        filteredTags = allTags.filter(tag => tag.toLowerCase().includes(val));
        renderDropdown();
    };

    searchInput.onfocus = function () {
        dropdown.classList.add('open');
    };
    searchInput.onblur = function () {
        setTimeout(() => dropdown.classList.remove('open'), 150);
    };

    renderDropdown();
    renderSelectedTags();
}

async function loadDashboard() {
    showLoading(true);
    try {
        const params = new URLSearchParams();
        if (currentFilters.projects.length > 0) params.append('project', currentFilters.projects.join(','));
        if (currentFilters.tags.length > 0) params.append('tags', currentFilters.tags.join(','));
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.statuses.length > 0) params.append('status', currentFilters.statuses.join(','));

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
    currentFilters.startDate = document.getElementById('startDate').value;
    // Add +1 day to end date so the backend's <= filter includes the entire selected day
    const endDateVal = document.getElementById('endDate').value;
    if (endDateVal) {
        const endDateObj = new Date(endDateVal);
        endDateObj.setDate(endDateObj.getDate() + 1);
        const ey = endDateObj.getFullYear();
        const em = String(endDateObj.getMonth() + 1).padStart(2, '0');
        const ed = String(endDateObj.getDate()).padStart(2, '0');
        currentFilters.endDate = `${ey}-${em}-${ed}`;
    } else {
        currentFilters.endDate = '';
    }
    // Note: projects, tags and statuses are managed by their respective comboboxes

    loadDashboard();
    loadTestCasesByTime();
}

function clearFilters() {
    currentFilters = {
        projects: [],
        tags: [],
        statuses: [],
        startDate: '',
        endDate: ''
    };

    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
    document.getElementById('projectsSearch').value = '';
    document.getElementById('tagsSearch').value = '';
    document.getElementById('statusSearch').value = '';

    // Re-render all comboboxes
    renderProjectsFilter();
    renderTagsFilter();
    renderStatusesFilter();

    loadDashboard();
    loadTestCasesByTime();
}

async function refreshData() {
    const refreshBtn = document.getElementById('refreshBtn');
    try {
        // Show loading state
        refreshBtn.disabled = true;
        refreshBtn.innerHTML = '⟳ Refreshing...';
        showLoadingSpinner(true);

        // Call refresh endpoint
        const response = await fetch(`${API_BASE_URL}/refresh`, { method: 'POST' });
        if (!response.ok) throw new Error('Refresh request failed');

        // Reload all data
        await loadProjects();
        await loadTags();
        await loadDashboard();
        await loadTestCasesByTime();

        // Update last updated time
        updateLastUpdatedTime();

        showSuccess('Data refreshed successfully');
    } catch (error) {
        console.error('Error refreshing data:', error);
        showError('Failed to refresh data');
    } finally {
        // Restore button state
        refreshBtn.disabled = false;
        refreshBtn.innerHTML = 'Refresh';
        showLoadingSpinner(false);
    }
}

function updateLastUpdatedTime() {
    const now = new Date();
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const seconds = String(now.getSeconds()).padStart(2, '0');
    document.getElementById('lastUpdate').textContent = `Last updated: ${hours}:${minutes}:${seconds}`;
}

function showLoadingSpinner(show) {
    const spinner = document.getElementById('loadingSpinner');
    if (spinner) {
        spinner.style.display = show ? 'block' : 'none';
    }
}

async function loadTestCasesByTime() {
    const container = document.getElementById('groupedByTimeContainer');
    if (!container) return;

    container.innerHTML = '<p>Loading test cases by time...</p>';

    try {
        const params = new URLSearchParams();
        if (currentFilters.projects.length > 0) params.append('project', currentFilters.projects.join(','));
        if (currentFilters.tags.length > 0) params.append('tags', currentFilters.tags.join(','));
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.statuses.length > 0) params.append('status', currentFilters.statuses.join(','));

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
        // Extract unique projects from all test cases in this group, show only last segment (e.g. "Nova/Feature/BES" → "BES")
        const uniqueProjects = [...new Set(group.testCases.map(test => test.project).filter(Boolean).map(p => p.split('/').pop()))].sort();
        // Extract unique tags from all test cases in this group
        const uniqueTags = [...new Set(group.testCases.flatMap(test => test.tags || []))].sort();
        const hasMetaRow = uniqueProjects.length > 0 || uniqueTags.length > 0;
        return `
            <div class="time-group-card" onclick="showTimeGroupDetails(${index})" style="cursor: pointer;">
                <div class="time-group-header">
                    <div style="display: flex; justify-content: space-between; align-items: center;">
                        <h3 style="margin: 0;">${group.timeGroup}</h3>
                        <span class="pass-rate" style="color: ${passRateColor};">${group.passRate.toFixed(1)}%</span>
                    </div>
                    ${hasMetaRow ? `
                    <div style="margin-top: 8px; padding-top: 8px; border-top: 1px solid #f0f0f5; display: flex; flex-direction: column; gap: 6px;">
                        ${uniqueProjects.length > 0 ? `<div style="display: flex; flex-wrap: wrap; gap: 4px;">${uniqueProjects.map(p => `<span class="project-chip">${escapeHtml(p)}</span>`).join('')}</div>` : ''}
                        ${uniqueTags.length > 0 ? `<div style="display: flex; flex-wrap: wrap; gap: 4px;">${uniqueTags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('')}</div>` : ''}
                    </div>
                    ` : ''}
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
                    <th>Project</th>
                    <th>Tags</th>
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
        const uniqueProjects = [...new Set(group.testCases.map(test => test.project).filter(Boolean).map(p => p.split('/').pop()))].sort();
        const uniqueTags = [...new Set(group.testCases.flatMap(test => test.tags || []))].sort();
        return `
                        <tr class="${rowClass}">
                            <td><strong>${escapeHtml(group.timeGroup)}</strong></td>
                            <td><div style="display: flex; flex-wrap: wrap; gap: 4px;">${uniqueProjects.map(p => `<span class="project-chip">${escapeHtml(p)}</span>`).join('') || '-'}</div></td>
                            <td><div style="display: flex; flex-wrap: wrap; gap: 4px;">${uniqueTags.map(tag => `<span class="tag">${escapeHtml(tag)}</span>`).join('') || '-'}</div></td>
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

    container.innerHTML = steps.map((step, index) => `
        <div class="step">
            <div class="step-name">${escapeHtml(step.name || 'Unknown Step')}</div>
            <div class="step-status">
                <span class="step-status-badge ${(step.status || 'passed').toLowerCase()}">
                    ${(step.status || 'passed').toUpperCase()}
                </span>
                ${step.stop && step.start ? `<span class="step-duration">${step.stop - step.start}ms</span>` : ''}
            </div>
            ${step.screenshotData ? `
                <div class="screenshot-section">
                    <h5 style="margin: 10px 0 8px 0; font-size: 12px; color: #666;">Screenshot</h5>
                    <div class="screenshot-thumbnail" onclick="openImageLightboxFromBase64('data:image/png;base64,${step.screenshotData}', 'Step ${index + 1} Screenshot')" style="cursor: pointer;">
                        <img src="data:image/png;base64,${step.screenshotData}" alt="Step ${index + 1} Screenshot" style="cursor: pointer; max-width: 100%; height: auto; border-radius: 4px;">
                    </div>
                </div>
            ` : ''}
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

function openImageLightboxFromBase64(base64Data, fileName) {
    document.getElementById('lightboxImage').src = base64Data;
    document.getElementById('lightboxFileName').textContent = fileName;
    document.getElementById('imageLightbox').style.display = 'flex';
    document.body.style.overflow = 'hidden';
}

function updateCharts(data) {
    // Calculate daily totals from test runs
    const dailyTotals = {};
    if (data.testRuns && Array.isArray(data.testRuns)) {
        data.testRuns.forEach(run => {
            const date = new Date(run.startTime);
            // Format as MM/DD/YYYY for consistent sorting
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const year = date.getFullYear();
            const dateStr = `${month}/${day}/${year}`;

            if (!dailyTotals[dateStr]) {
                dailyTotals[dateStr] = {
                    total: 0,
                    passed: 0,
                    failed: 0,
                    skipped: 0,
                    broken: 0
                };
            }
            // Use the counts from the testRun object, not from results.length
            const testCount = (run.passedCount || 0) + (run.failedCount || 0) + (run.skippedCount || 0) + (run.brokenCount || 0);
            dailyTotals[dateStr].total += testCount;
            dailyTotals[dateStr].passed += run.passedCount || 0;
            dailyTotals[dateStr].failed += run.failedCount || 0;
            dailyTotals[dateStr].skipped += run.skippedCount || 0;
            dailyTotals[dateStr].broken += run.brokenCount || 0;
        });
    }

    updateStatusChart(dailyTotals);
    updateBreakdownChart(data.statusCounts || {});
}

function updateStatusChart(dailyTotals) {
    const ctx = document.getElementById('statusChart');
    if (!ctx) return;

    // Sort dates and prepare data
    const dates = Object.keys(dailyTotals).sort((a, b) => new Date(a) - new Date(b));
    const totals = dates.map(date => dailyTotals[date].total);
    const passed = dates.map(date => dailyTotals[date].passed);
    const failed = dates.map(date => dailyTotals[date].failed);
    const skipped = dates.map(date => dailyTotals[date].skipped);
    const broken = dates.map(date => dailyTotals[date].broken);

    if (statusChart) {
        statusChart.destroy();
    }

    statusChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: dates.length > 0 ? dates : ['No Data'],
            datasets: [
                {
                    label: 'Passed',
                    data: passed,
                    backgroundColor: '#059669',
                    borderColor: '#059669',
                    borderWidth: 0,
                    borderRadius: 6
                },
                {
                    label: 'Failed',
                    data: failed,
                    backgroundColor: '#dc2626',
                    borderColor: '#dc2626',
                    borderWidth: 0,
                    borderRadius: 6
                },
                {
                    label: 'Skipped',
                    data: skipped,
                    backgroundColor: '#d97706',
                    borderColor: '#d97706',
                    borderWidth: 0,
                    borderRadius: 6
                },
                {
                    label: 'Broken',
                    data: broken,
                    backgroundColor: '#8b5cf6',
                    borderColor: '#8b5cf6',
                    borderWidth: 0,
                    borderRadius: 6
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: true,
            plugins: {
                legend: {
                    position: 'top',
                    labels: {
                        font: { size: 12, weight: '600' },
                        padding: 15,
                        color: '#1f2937'
                    }
                },
                tooltip: {
                    callbacks: {
                        afterLabel: function (context) {
                            if (context.datasetIndex === 3) { // After broken (last dataset)
                                const index = context.dataIndex;
                                const total = passed[index] + failed[index] + skipped[index] + broken[index];
                                return 'Total: ' + total;
                            }
                        }
                    }
                }
            },
            scales: {
                x: {
                    stacked: true,
                    ticks: {
                        color: '#6b7280',
                        font: { size: 12 }
                    },
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
                    }
                },
                y: {
                    stacked: true,
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1,
                        color: '#6b7280',
                        font: { size: 12 }
                    },
                    grid: {
                        color: 'rgba(0, 0, 0, 0.05)'
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

// Export to Excel functionality
async function generateExcelReport() {
    try {
        showLoading(true);

        // Fetch current filtered data
        const params = new URLSearchParams();
        if (currentFilters.project) params.append('project', currentFilters.project);
        if (currentFilters.tags.length > 0) params.append('tags', currentFilters.tags.join(','));
        if (currentFilters.startDate) params.append('startDate', currentFilters.startDate);
        if (currentFilters.endDate) params.append('endDate', currentFilters.endDate);
        if (currentFilters.status) params.append('status', currentFilters.status);

        const response = await fetch(`${API_BASE_URL}/results?${params}`);
        if (!response.ok) throw new Error('Failed to fetch results');

        const results = await response.json();

        // Create workbook
        const wb = XLSX.utils.book_new();
        wb.props = {
            title: 'Allure Test Report',
            author: 'Allure Dashboard',
            created: new Date()
        };

        // Sheet 1: Summary
        const summaryData = createSummarySummary(results);
        const summarySheet = XLSX.utils.json_to_sheet(summaryData);
        applyHeaderStyle(summarySheet, ['Metric', 'Value']);
        setSummaryColumnWidths(summarySheet);
        XLSX.utils.book_append_sheet(wb, summarySheet, 'Summary');

        // Sheet 2: By Date/Time
        const byTime = createByTimeData(results);
        const timeSheet = XLSX.utils.json_to_sheet(byTime);
        applyHeaderStyle(timeSheet, ['Date & Time', 'Total', 'Passed', 'Failed', 'Skipped', 'Broken', 'Pass Rate (%)']);
        applyDataStyle(timeSheet, byTime.length);
        setColumnWidths(timeSheet, [25, 10, 10, 10, 10, 10, 15]);
        XLSX.utils.book_append_sheet(wb, timeSheet, 'By Date & Time');

        // Sheet 3: By Project
        const byProject = createByProjectData(results);
        const projectSheet = XLSX.utils.json_to_sheet(byProject);
        applyHeaderStyle(projectSheet, ['Project', 'Total', 'Passed', 'Failed', 'Skipped', 'Broken', 'Pass Rate (%)']);
        applyDataStyle(projectSheet, byProject.length);
        setColumnWidths(projectSheet, [20, 10, 10, 10, 10, 10, 15]);
        XLSX.utils.book_append_sheet(wb, projectSheet, 'By Project');

        // Sheet 4: By Status
        const byStatus = createByStatusData(results);
        const statusSheet = XLSX.utils.json_to_sheet(byStatus);
        applyHeaderStyle(statusSheet, ['Status', 'Count', 'Total Duration (ms)', 'Average Duration (ms)', 'Percentage (%)']);
        applyDataStyle(statusSheet, byStatus.length);
        setColumnWidths(statusSheet, [15, 12, 20, 22, 18]);
        XLSX.utils.book_append_sheet(wb, statusSheet, 'By Status');

        // Sheet 5: All Results Details
        const details = createDetailsData(results);
        const detailsSheet = XLSX.utils.json_to_sheet(details);
        applyHeaderStyle(detailsSheet, ['Test Name', 'Status', 'Project', 'Duration (ms)', 'Timestamp', 'Tags']);
        applyStatusColorCondition(detailsSheet, details.length);
        setColumnWidths(detailsSheet, [35, 12, 15, 15, 20, 25]);
        XLSX.utils.book_append_sheet(wb, detailsSheet, 'All Results');

        // Generate filename with timestamp
        const now = new Date();
        const filename = `AllureReport_${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}_${String(now.getHours()).padStart(2, '0')}-${String(now.getMinutes()).padStart(2, '0')}.xlsx`;

        // Download
        XLSX.writeFile(wb, filename);
        showSuccess('Report exported successfully');
    } catch (error) {
        console.error('Error generating report:', error);
        showError('Failed to generate report');
    } finally {
        showLoading(false);
    }
}

// Apply header styling to worksheet
function applyHeaderStyle(sheet, headers) {
    const headerStyle = {
        fill: { fgColor: { rgb: 'FF667EEA' } },
        font: { bold: true, color: { rgb: 'FFFFFFFF' }, size: 12 },
        alignment: { horizontal: 'center', vertical: 'center', wrapText: true },
        border: {
            top: { style: 'thin', color: { rgb: 'FF000000' } },
            bottom: { style: 'thin', color: { rgb: 'FF000000' } },
            left: { style: 'thin', color: { rgb: 'FF000000' } },
            right: { style: 'thin', color: { rgb: 'FF000000' } }
        }
    };

    const range = XLSX.utils.decode_range(sheet['!ref']);
    for (let C = range.s.c; C <= range.e.c; ++C) {
        const cellAddress = XLSX.utils.encode_col(C) + '1';
        if (!sheet[cellAddress]) continue;
        sheet[cellAddress].s = headerStyle;
    }
}

// Apply data row styling
function applyDataStyle(sheet, rowCount) {
    const dataStyle = {
        alignment: { horizontal: 'center', vertical: 'center', wrapText: false },
        border: {
            top: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            bottom: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            left: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            right: { style: 'thin', color: { rgb: 'FFE5E7EB' } }
        }
    };

    const alternateRowStyle = {
        fill: { fgColor: { rgb: 'FFF9FAFB' } },
        ...dataStyle
    };

    const range = XLSX.utils.decode_range(sheet['!ref']);
    for (let R = 2; R <= rowCount + 1; ++R) {
        for (let C = range.s.c; C <= range.e.c; ++C) {
            const cellAddress = XLSX.utils.encode_cell({ r: R - 1, c: C });
            if (!sheet[cellAddress]) continue;
            sheet[cellAddress].s = R % 2 === 0 ? alternateRowStyle : dataStyle;
        }
    }
}

// Apply status-based coloring for status column
function applyStatusColorCondition(sheet, rowCount) {
    const statusColors = {
        PASSED: 'FF059669',
        FAILED: 'FFDC2626',
        SKIPPED: 'FFD97706',
        BROKEN: 'FF7C3AED'
    };

    const dataStyle = {
        alignment: { horizontal: 'center', vertical: 'center' },
        border: {
            top: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            bottom: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            left: { style: 'thin', color: { rgb: 'FFE5E7EB' } },
            right: { style: 'thin', color: { rgb: 'FFE5E7EB' } }
        }
    };

    const range = XLSX.utils.decode_range(sheet['!ref']);
    for (let R = 2; R <= rowCount + 1; ++R) {
        for (let C = range.s.c; C <= range.e.c; ++C) {
            const cellAddress = XLSX.utils.encode_cell({ r: R - 1, c: C });
            if (!sheet[cellAddress]) continue;

            // Status column is column B (index 1)
            if (C === 1) {
                const status = sheet[cellAddress].v;
                const bgColor = statusColors[status] || 'FFF3F4F6';
                sheet[cellAddress].s = {
                    fill: { fgColor: { rgb: bgColor } },
                    font: { bold: true, color: { rgb: 'FFFFFFFF' }, size: 11 },
                    alignment: { horizontal: 'center', vertical: 'center' },
                    border: dataStyle.border
                };
            } else {
                const alternateStyle = R % 2 === 0 ? { fill: { fgColor: { rgb: 'FFF9FAFB' } } } : {};
                sheet[cellAddress].s = { ...dataStyle, ...alternateStyle };
            }
        }
    }
}

// Set column widths
function setColumnWidths(sheet, widths) {
    sheet['!cols'] = widths.map(w => ({ wch: w }));
}

// Set summary specific column widths
function setSummaryColumnWidths(sheet) {
    sheet['!cols'] = [{ wch: 25 }, { wch: 20 }];
}

function createSummarySummary(results) {
    const total = results.length;
    const passed = results.filter(r => r.status === 'PASSED').length;
    const failed = results.filter(r => r.status === 'FAILED').length;
    const skipped = results.filter(r => r.status === 'SKIPPED').length;
    const broken = results.filter(r => r.status === 'BROKEN').length;
    const passRate = total > 0 ? ((passed / total) * 100).toFixed(2) : 0;
    const totalDuration = results.reduce((sum, r) => sum + (r.duration || 0), 0);
    const avgDuration = total > 0 ? (totalDuration / total).toFixed(2) : 0;

    return [
        { Metric: '📊 Total Tests', Value: total },
        { Metric: '✅ Passed', Value: passed },
        { Metric: '❌ Failed', Value: failed },
        { Metric: '⏭️  Skipped', Value: skipped },
        { Metric: '🔧 Broken', Value: broken },
        { Metric: '📈 Pass Rate (%)', Value: passRate },
        { Metric: '⏱️  Total Duration (ms)', Value: totalDuration },
        { Metric: '⏱️  Avg Duration (ms)', Value: avgDuration },
        { Metric: '📅 Report Generated', Value: new Date().toLocaleString('tr-TR') }
    ];
}

function createByTimeData(results) {
    const grouped = {};

    results.forEach(result => {
        const date = new Date(result.timestamp);
        const timeKey = date.toLocaleDateString('tr-TR') + ' ' + String(date.getHours()).padStart(2, '0') + ':' + String(date.getMinutes()).padStart(2, '0');

        if (!grouped[timeKey]) {
            grouped[timeKey] = { passed: 0, failed: 0, skipped: 0, broken: 0, total: 0, duration: 0 };
        }
        grouped[timeKey][result.status.toLowerCase()]++;
        grouped[timeKey].total++;
        grouped[timeKey].duration += result.duration || 0;
    });

    return Object.entries(grouped).map(([timeGroup, stats]) => ({
        'Date & Time': timeGroup,
        'Total': stats.total,
        'Passed': stats.passed,
        'Failed': stats.failed,
        'Skipped': stats.skipped,
        'Broken': stats.broken,
        'Pass Rate (%)': parseFloat(((stats.passed / stats.total) * 100).toFixed(2))
    })).sort((a, b) => new Date(b['Date & Time']) - new Date(a['Date & Time']));
}

function createByProjectData(results) {
    const grouped = {};

    results.forEach(result => {
        const project = result.project || 'Unknown';

        if (!grouped[project]) {
            grouped[project] = { passed: 0, failed: 0, skipped: 0, broken: 0, total: 0 };
        }
        grouped[project][result.status.toLowerCase()]++;
        grouped[project].total++;
    });

    return Object.entries(grouped).map(([project, stats]) => ({
        'Project': project,
        'Total': stats.total,
        'Passed': stats.passed,
        'Failed': stats.failed,
        'Skipped': stats.skipped,
        'Broken': stats.broken,
        'Pass Rate (%)': parseFloat(((stats.passed / stats.total) * 100).toFixed(2))
    })).sort((a, b) => b.Total - a.Total);
}

function createByStatusData(results) {
    const grouped = {};

    results.forEach(result => {
        const status = result.status;

        if (!grouped[status]) {
            grouped[status] = { count: 0, duration: 0 };
        }
        grouped[status].count++;
        grouped[status].duration += result.duration || 0;
    });

    const statusOrder = ['PASSED', 'FAILED', 'BROKEN', 'SKIPPED'];
    return Object.entries(grouped)
        .sort((a, b) => statusOrder.indexOf(a[0]) - statusOrder.indexOf(b[0]))
        .map(([status, stats]) => ({
            'Status': status,
            'Count': stats.count,
            'Total Duration (ms)': stats.duration,
            'Average Duration (ms)': parseFloat((stats.duration / stats.count).toFixed(2)),
            'Percentage (%)': parseFloat(((stats.count / results.length) * 100).toFixed(2))
        }));
}

function createDetailsData(results) {
    return results
        .sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp))
        .map(result => ({
            'Test Name': result.name,
            'Status': result.status,
            'Project': result.project || '-',
            'Duration (ms)': result.duration || 0,
            'Timestamp': formatDate(result.timestamp),
            'Tags': result.tags ? result.tags.join(', ') : '-'
        }));
}

// Close modal on background click and initialize on page load
document.addEventListener('DOMContentLoaded', function () {
    document.getElementById('detailsModal').addEventListener('click', function (e) {
        if (e.target === this) {
            closeDetailsModal();
        }
    });

    document.getElementById('timeGroupModal').addEventListener('click', function (e) {
        if (e.target === this) {
            closeTimeGroupModal();
        }
    });

    init();
});


