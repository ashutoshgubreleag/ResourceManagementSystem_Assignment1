using ResourceManagementSystem.Models;
using ResourceManagementSystem.Repositories;
using EmployeeModel = ResourceManagementSystem.Models.Employee;

namespace ResourceManagementSystem.Services.Reports
{
    public class ReportService : IReportService
    {
        private const int BenchThresholdPercentage = 50;

        private readonly IBaseEntityRepository<EmployeeModel> _employeeRepository;
        private readonly IBaseEntityRepository<ClientProject> _projectRepository;
        private readonly IBaseEntityRepository<ProjectAllocation> _allocationRepository;

        public ReportService(
            IBaseEntityRepository<EmployeeModel> employeeRepository,
            IBaseEntityRepository<ClientProject> projectRepository,
            IBaseEntityRepository<ProjectAllocation> allocationRepository)
        {
            _employeeRepository = employeeRepository;
            _projectRepository = projectRepository;
            _allocationRepository = allocationRepository;
        }

        public void ShowBenchReport()
        {
            Console.WriteLine("-- Bench Report --");
            Console.WriteLine($"Active employees allocated below {BenchThresholdPercentage}%:");

            var allocations = _allocationRepository.GetAll().ToList();
            var employees = _employeeRepository.GetAll()
                .Where(employee => employee.Status == EmployeeStatus.Active)
                .Select(employee => new
                {
                    Employee = employee,
                    Allocation = allocations
                        .Where(allocation => allocation.EmployeeId == employee.Id)
                        .Sum(allocation => allocation.AllocationPercentage)
                })
                .Where(item => item.Allocation < BenchThresholdPercentage)
                .ToList();

            if (employees.Count == 0)
            {
                Console.WriteLine("No employees meet the bench criteria.");
            }
            else
            {
                foreach (var item in employees)
                {
                    Console.WriteLine($"Id: {item.Employee.Id}, Name: {item.Employee.Name}, " +
                        $"Department: {item.Employee.Department}, Allocation: {item.Allocation}%");
                }
            }

            WaitForContinue();
        }

        public void ShowProjectStaffingSummary()
        {
            Console.WriteLine("-- Project Staffing Summary --");
            var employees = _employeeRepository.GetAll().ToDictionary(employee => employee.Id);
            var projects = _projectRepository.GetAll().ToList();
            var allocations = _allocationRepository.GetAll().ToList();

            if (projects.Count == 0)
            {
                Console.WriteLine("No projects found.");
            }
            else
            {
                foreach (var project in projects)
                {
                    var projectAllocations = allocations
                        .Where(allocation => allocation.ProjectId == project.Id)
                        .ToList();
                    var employeeCount = projectAllocations
                        .Select(allocation => allocation.EmployeeId)
                        .Distinct()
                        .Count();
                    var activeEmployeeCount = projectAllocations
                        .Where(allocation => employees.TryGetValue(allocation.EmployeeId, out var employee)
                            && employee.Status == EmployeeStatus.Active)
                        .Select(allocation => allocation.EmployeeId)
                        .Distinct()
                        .Count();
                    var totalAllocation = projectAllocations.Sum(allocation => allocation.AllocationPercentage);

                    Console.WriteLine($"Id: {project.Id}, Name: {project.Name}, Client: {project.Client}, " +
                        $"Status: {project.Status}, Employees: {employeeCount} " +
                        $"({activeEmployeeCount} active), Allocations: {projectAllocations.Count}, " +
                        $"Total allocation: {totalAllocation}%");
                }
            }

            WaitForContinue();
        }

        public void ShowDepartmentUtilisation()
        {
            Console.WriteLine("-- Utilisation by Department --");
            var allocations = _allocationRepository.GetAll().ToList();
            var employees = _employeeRepository.GetAll()
                .Where(employee => employee.Status == EmployeeStatus.Active)
                .ToList();

            var departments = employees
                .GroupBy(employee => employee.Department)
                .OrderBy(group => group.Key)
                .ToList();

            if (departments.Count == 0)
            {
                Console.WriteLine("No active employees found.");
            }
            else
            {
                foreach (var department in departments)
                {
                    var employeeUtilisation = department
                        .Select(employee => Math.Min(100, allocations
                            .Where(allocation => allocation.EmployeeId == employee.Id)
                            .Sum(allocation => allocation.AllocationPercentage)))
                        .ToList();
                    var departmentUtilisation = employeeUtilisation.Average();

                    Console.WriteLine($"Department: {department.Key}, Employees: {department.Count()}, " +
                        $"Utilisation: {departmentUtilisation:F2}%");
                }
            }

            WaitForContinue();
        }

        private static void WaitForContinue()
        {
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
    }
}
