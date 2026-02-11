## Allure Dashboard Project

This project provides a .NET 10 backend with an HTML/CSS/JavaScript frontend for visualizing and analyzing Allure test report results.

### Project Information
- **Backend**: ASP.NET Core (.NET 10)
- **Frontend**: HTML5, CSS3, JavaScript (Vanilla)
- **Purpose**: Dashboard for viewing Allure test results with filtering capabilities
- **Key Features**: File watching, project/tag/date filtering, historical tracking

### Architecture
- File watcher monitors a local directory for new Allure JSON report files
- REST API provides filtered access to test results
- Interactive web dashboard displays statistics and detailed results
- Support for filtering by project, tags, dates, and test status

### Getting Started
1. Backend: Navigate to `backend/Allure.Dashboard` and run `dotnet run`
2. Frontend: Open `frontend/index.html` in a browser (or serve via http-server)
3. Configure Allure reports path in `appsettings.json`
4. Place Allure JSON files in the configured directory

See [README.md](../README.md) for detailed setup instructions.
