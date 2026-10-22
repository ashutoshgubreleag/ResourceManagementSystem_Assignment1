# Resource Management System

A .NET 10 console application for managing employees, client projects, project allocations, skills, and workforce reports.

## How to run

Prerequisites:

- .NET 10 SDK
- Visual Studio 2026 or another .NET-compatible IDE

From the repository root:

```powershell
dotnet restore .\ResourceManagementSystem\ResourceManagementSystem.csproj
dotnet run --project .\ResourceManagementSystem\ResourceManagementSystem.csproj
```

The application uses the generic JSON repository by default. Employee data is stored in `ResourceManagementSystem/Employee.json`. Projects and allocations are stored in `ClientProject.json` and `ProjectAllocation.json` when created.

To use EF Core with SQLite, change the registrations in `ResourceManagementSystem/Program.cs`:

```csharp
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=ResourceManagementSystem.db"));
services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseEntityRepository<>));
```

Create or migrate the database schema before using the EF Core backend.

## Implemented features

### Employee management

- Add employees with name, email, position, department, salary, and skills.
- Assign skill proficiency levels from 1 through 5.
- Update employee details and skills.
- Deactivate employees using the generic repository's `GetById` and `Update` operations.
- List employees with their status and skills.

### Project management

- Create client projects with a name, client, date range, status, and required skills.
- Set a minimum proficiency level from 1 through 5 for every required skill.
- List all projects.

### Employee allocation

- Allocate an employee to a project for a date range and percentage.
- Enforce these rules:
  - Allocation percentage must be between 1% and 100%.
  - Allocation dates must fall within the project dates.
  - Overlapping employee allocation cannot exceed 100%.
  - Inactive employees cannot be allocated.
- Release all allocations for an employee and project using project ID and employee ID.
- Display simulated notifications after successful allocation and release.

### Employee suggestions

Suggest active employees who have all required project skills, meet the minimum proficiency levels, and have available capacity during the project period.

### Reports

- **Bench report**: active employees with recorded allocation below the defined 50% threshold.
- **Project staffing summary**: employee counts, allocation counts, and total allocation per project.
- **Utilisation by department**: average active-employee utilization by department, with each employee capped at 100%.

## Console menu

```text
1) Add employee
2) Update employee
3) Deactivate an employee
4) Get all employees
5) Create client project
6) Get all projects
7) Allocate employee to project
8) Suggest available employees for project
9) Release employee allocation
10) Bench report
11) Project staffing summary
12) Utilisation by department
Q) Quit
```

## Sample console session

```text
Choose an option:
5) Create client project
Enter choice: 5

-- Create Client Project --
Project name: Website Redesign
Client: Contoso Ltd
Start date (yyyy-MM-dd): 2026-04-01
End date (yyyy-MM-dd): 2026-06-30
Number of required skills: 1
Required skill 1 name: C#
Minimum proficiency for C# (1-5): 4
Created project with Id 1. Press Enter to continue...

Choose an option:
8) Suggest available employees for project
Enter choice: 8

-- Suggest Available Employees --
Project Id: 1
Id: 1, Name: John Doe, Skills: C# (4/5), SQL (3/5)
Press Enter to continue...

Choose an option:
7) Allocate employee to project
Enter choice: 7

-- Allocate Employee --
Project Id: 1
Employee Id: 1
Allocation start date (yyyy-MM-dd): 2026-04-01
Allocation end date (yyyy-MM-dd): 2026-06-30
Allocation percentage (1-100): 50
[Notification] John Doe was allocated to project 'Website Redesign' from 2026-04-01 to 2026-06-30 at 50% allocation.
Employee allocated successfully. Press Enter to continue...

Choose an option:
10) Bench report
Enter choice: 10

-- Bench Report --
Active employees allocated below 50%:
Id: 2, Name: Smith Watts, Department: IT, Allocation: 0%
Press Enter to continue...

Choose an option:
11) Project staffing summary
Enter choice: 11

-- Project Staffing Summary --
Id: 1, Name: Website Redesign, Client: Contoso Ltd, Status: Planned, Employees: 1 (1 active), Allocations: 1, Total allocation: 50%
Press Enter to continue...

Choose an option:
9) Release employee allocation
Enter choice: 9

-- Release Employee Allocation --
Project Id: 1
Employee Id: 1
[Notification] John Doe was released from project 'Website Redesign' for the allocation period 2026-04-01 to 2026-06-30.
Employee allocation released. Press Enter to continue...
```

## Repository implementations

Services depend on `IBaseEntityRepository<TEntity>`, so persistence can be changed through dependency injection without changing service code:

- `BaseJsonRepository<TEntity>` for JSON files.
- `BaseEntityRepository<TEntity>` for EF Core and SQLite.

The default registration in `Program.cs` uses `BaseJsonRepository<TEntity>`.

This line is added to add the README.md file in feature branch with some changes.