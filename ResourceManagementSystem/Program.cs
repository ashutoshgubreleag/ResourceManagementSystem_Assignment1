using System;
using System.Globalization;
using ResourceManagementSystem.Models;
// using ResourceManagementSystem.Repositories.Employee; (removed - JSON-backed repository now registered via BaseJsonRepository)
using ResourceManagementSystem.Services.Employee;
using ResourceManagementSystem.Services.Project;
using ResourceManagementSystem.Services.Notifications;
using ResourceManagementSystem.Services.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ResourceManagementSystem.Data;
using ResourceManagementSystem.Repositories;

var services = new ServiceCollection();
//services.AddDbContext<ApplicationDbContext>(opts => opts.UseSqlite("Data Source=ResourceManagementSystem.db"));
//services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseEntityRepository<>));
// To use JSON-backed repository for all entities, replace BaseEntityRepository registration with BaseJsonRepository:
 services.AddScoped(typeof(IBaseEntityRepository<>), typeof(BaseJsonRepository<>));
// To use a specific JSON-backed implementation for Employee only, register:
// services.AddScoped<IBaseEntityRepository<ResourceManagementSystem.Models.Employee>>(sp => new BaseJsonRepository<ResourceManagementSystem.Models.Employee>("Employee.json"));
services.AddScoped<IEmployeeService, EmployeeService>();
services.AddScoped<IProjectService, ProjectService>();
services.AddScoped<IReportService, ReportService>();
services.AddSingleton<IAllocationNotificationService, SimulatedAllocationNotificationService>();
using var serviceProvider = services.BuildServiceProvider();
var service = serviceProvider.GetRequiredService<IEmployeeService>();
var projectService = serviceProvider.GetRequiredService<IProjectService>();
var reportService = serviceProvider.GetRequiredService<IReportService>();

while (true)
{
    Console.Clear();
    Console.WriteLine("Choose an option:");
    Console.WriteLine("1) Add employee");
    Console.WriteLine("2) Update employee");
    Console.WriteLine("3) Deactivate an employee");
    Console.WriteLine("4) Get all employees");
    Console.WriteLine("5) Create client project");
    Console.WriteLine("6) Get all projects");
    Console.WriteLine("7) Allocate employee to project");
    Console.WriteLine("8) Suggest available employees for project");
    Console.WriteLine("9) Release employee allocation");
    Console.WriteLine("10) Bench report");
    Console.WriteLine("11) Project staffing summary");
    Console.WriteLine("12) Utilisation by department");
    Console.WriteLine("Q) Quit");
    Console.Write("Enter choice: ");
    var choice = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(choice)) continue;
    if (choice.Equals("Q", StringComparison.OrdinalIgnoreCase)) break;

    switch (choice.Trim())
    {
        case "1":
            service.AddEmployee();
            break;
        case "2":
            service.UpdateEmployee();
            break;
        case "3":
            service.DeactivateEmployee();
            break;
        case "4":
            service.ListEmployees();
            break;
        case "5":
            projectService.CreateProject();
            break;
        case "6":
            projectService.ListProjects();
            break;
        case "7":
            projectService.AllocateEmployee();
            break;
        case "8":
            projectService.SuggestAvailableEmployees();
            break;
        case "9":
            projectService.ReleaseEmployee();
            break;
        case "10":
            reportService.ShowBenchReport();
            break;
        case "11":
            reportService.ShowProjectStaffingSummary();
            break;
        case "12":
            reportService.ShowDepartmentUtilisation();
            break;
        default:
            Console.WriteLine("Invalid choice. Press Enter to continue...");
            Console.ReadLine();
            break;
    }
}
