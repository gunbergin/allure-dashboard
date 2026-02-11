# Allure Dashboard

A comprehensive web dashboard for viewing and analyzing Allure test report results with powerful filtering capabilities.

## Features

- 📊 **Dashboard View**: Display test results with statistics (passed, failed, skipped, broken tests)
- 🔍 **Advanced Filtering**: Filter by project, tags, date range, and test status
- 📈 **Historical Tracking**: Track test results over time
- 🔄 **Auto-refresh**: File watcher automatically detects new Allure report JSON files
- 📱 **Responsive Design**: Works on desktop and mobile devices
- ⚡ **Real-time Updates**: Automatic data refresh when new test reports are detected

## Architecture

### Backend (.NET 10)
- **ASP.NET Core** REST API
- **File Watcher**: Monitors a local directory for new Allure JSON files
- **JSON Processing**: Parses Allure test result JSON files
- **Filtering Engine**: Provides filtered results based on multiple criteria

### Frontend (HTML/CSS/JavaScript)
- **Modern UI**: Clean and intuitive dashboard design
- **Responsive Layout**: Sidebar filters with main content area
- **Real-time Statistics**: Shows test counts and pass rate
- **Interactive Tables**: Sortable and filterable results table

## Project Structure

```
allure-dashboard/
├── backend/
│   └── Allure.Dashboard/
│       ├── Controllers/
│       │   └── DashboardController.cs
│       ├── Services/
│       │   ├── AllureService.cs
│       │   ├── IAllureService.cs
│       │   ├── FileWatcherService.cs
│       │   └── IFileWatcherService.cs
│       ├── Models/
│       │   └── TestResult.cs
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Allure.Dashboard.csproj
│       └── Properties/
│           └── launchSettings.json
├── frontend/
│   ├── index.html
│   ├── styles.css
│   └── script.js
└── README.md
```

## Setup Instructions

### Prerequisites

- .NET 10 SDK installed ([get it here](https://dotnet.microsoft.com/download))
- A modern web browser

### Backend Setup

1. Navigate to the backend directory:
   ```bash
   cd backend/Allure.Dashboard
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Run the application:
   ```bash
   dotnet run
   ```

   The API will start on `http://localhost:5000`

### Frontend Setup

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```

2. Open `index.html` in your web browser or serve it with a local web server:
   ```bash
   # Using Python 3
   python -m http.server 8000
   
   # Using Node.js http-server
   npx http-server
   ```

   Access the dashboard at `http://localhost:8000`

## Configuration

### Allure Reports Directory

By default, the backend looks for Allure reports in `./allure-reports` directory. To change this:

1. Edit `backend/Allure.Dashboard/appsettings.json`:
   ```json
   {
     "AllureReportsPath": "/path/to/your/allure-reports"
   }
   ```

2. The directory structure should be:
   ```
   allure-reports/
   └── data/
       ├── test-results/
       │   ├── *.json (test result files)
       └── history.json (optional)
   ```

## API Endpoints

### Get Dashboard Data
```
GET /api/dashboard?project={project}&tags={tag1,tag2}&startDate={date}&endDate={date}&status={status}
```
Returns dashboard statistics and filtered test results

### Get Test Results
```
GET /api/results?project={project}&tags={tag1,tag2}&startDate={date}&endDate={date}&status={status}
```
Returns filtered list of test results

### Get Projects
```
GET /api/projects
```
Returns list of all available projects

### Get Tags
```
GET /api/tags
```
Returns list of all available tags

### Refresh Data
```
POST /api/refresh
```
Manually trigger data refresh from files

## Allure Report JSON Format

The dashboard expects test result JSON files in Allure format. Each result file should contain:

```json
{
  "uuid": "unique-id",
  "historyId": "history-id",
  "name": "Test Name",
  "fullName": "com.example.TestClass.testName",
  "status": "PASSED|FAILED|SKIPPED|BROKEN",
  "labels": [
    "tag:value",
    "feature:login"
  ],
  "start": "2024-01-15T10:30:00Z",
  "stop": "2024-01-15T10:30:05Z",
  "steps": []
}
```

## Usage

1. **View Dashboard**: Open the frontend to see all test results and statistics
2. **Filter Results**: Use the sidebar filters to narrow down results by:
   - Project
   - Test Status (Passed, Failed, Skipped, Broken)
   - Tags
   - Date Range
3. **Refresh Data**: Click the refresh button to reload data from the Allure reports directory
4. **Track History**: The dashboard automatically tracks all test results over time

## Filtering Guide

- **Project**: Filter results to a specific project
- **Status**: Show only tests with a specific status
- **Tags**: Filter by one or more tags (any tag match)
- **Date Range**: Filter results within a start and end date

## Troubleshooting

### Backend won't start
- Ensure .NET 10 SDK is installed: `dotnet --version`
- Check that port 5000 is not in use
- Review console output for specific error messages

### No data appears in dashboard
- Verify the allure-reports directory path is correct
- Check that JSON files exist in `allure-reports/data/test-results/`
- Click the refresh button to manually load data
- Check browser console for API errors

### CORS errors
- The backend is configured to allow all origins by default
- For production, update the CORS policy in `Program.cs`

## Development

### Adding Features

To add new filtering options or API endpoints:

1. Update models in `Models/TestResult.cs`
2. Modify filtering logic in `Services/AllureService.cs`
3. Add new controller methods in `Controllers/DashboardController.cs`
4. Update frontend UI in `frontend/index.html` and `frontend/script.js`

## License

MIT License - Feel free to use and modify as needed

## Support

For issues or feature requests, please check the project repository
