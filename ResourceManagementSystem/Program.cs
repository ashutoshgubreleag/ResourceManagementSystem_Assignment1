using System;
using System.Globalization;
using ResourceManagementSystem.Models;
// using ResourceManagementSystem.Repositories.Employee; (removed - JSON-backed repository now registered via BaseJsonRepository)
using ResourceManagementSystem.Services.Employee;
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
using var serviceProvider = services.BuildServiceProvider();
var service = serviceProvider.GetRequiredService<IEmployeeService>();

while (true)
{
    Console.Clear();
    Console.WriteLine("Choose an option:");
    Console.WriteLine("1) Add employee");
    Console.WriteLine("2) Update employee");
    Console.WriteLine("3) Deactivate an employee");
    Console.WriteLine("4) Get all employees");
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
        default:
            Console.WriteLine("Invalid choice. Press Enter to continue...");
            Console.ReadLine();
            break;
    }
}
