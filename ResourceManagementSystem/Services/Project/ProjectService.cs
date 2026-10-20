using System.Globalization;
using ResourceManagementSystem.Models;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Services.Notifications;
using EmployeeModel = ResourceManagementSystem.Models.Employee;

namespace ResourceManagementSystem.Services.Project
{
    public class ProjectService : IProjectService
    {
        private readonly IBaseEntityRepository<ClientProject> _projectRepository;
        private readonly IBaseEntityRepository<EmployeeModel> _employeeRepository;
        private readonly IBaseEntityRepository<ProjectAllocation> _allocationRepository;
        private readonly IAllocationNotificationService _notificationService;

        public ProjectService(
            IBaseEntityRepository<ClientProject> projectRepository,
            IBaseEntityRepository<EmployeeModel> employeeRepository,
            IBaseEntityRepository<ProjectAllocation> allocationRepository,
            IAllocationNotificationService notificationService)
        {
            _projectRepository = projectRepository;
            _employeeRepository = employeeRepository;
            _allocationRepository = allocationRepository;
            _notificationService = notificationService;
        }

        public void CreateProject()
        {
            Console.WriteLine("-- Create Client Project --");
            Console.Write("Project name: ");
            var name = Console.ReadLine()?.Trim() ?? string.Empty;
            Console.Write("Client: ");
            var client = Console.ReadLine()?.Trim() ?? string.Empty;
            var startDate = ReadDate("Start date (yyyy-MM-dd): ");
            var endDate = ReadDate("End date (yyyy-MM-dd): ");

            while (endDate < startDate)
            {
                Console.WriteLine("End date must be on or after the start date.");
                endDate = ReadDate("End date (yyyy-MM-dd): ");
            }

            var project = new ClientProject
            {
                Name = name,
                Client = client,
                StartDate = startDate,
                EndDate = endDate,
                Status = ProjectStatus.Planned,
                RequiredSkills = ReadRequiredSkills()
            };

            var added = _projectRepository.Add(project);
            Console.WriteLine($"Created project with Id {added.Id}. Press Enter to continue...");
            Console.ReadLine();
        }

        public void ReleaseEmployee()
        {
            Console.WriteLine("-- Release Employee Allocation --");
            var projectId = ReadInteger("Project Id: ");
            var employeeId = ReadInteger("Employee Id: ");
            var project = _projectRepository.GetById(projectId);
            var employee = _employeeRepository.GetById(employeeId);

            if (project == null || employee == null)
            {
                Console.WriteLine("The project or employee was not found. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var allocations = _allocationRepository.GetAll()
                .Where(allocation => allocation.ProjectId == projectId && allocation.EmployeeId == employeeId)
                .ToList();

            if (allocations.Count == 0)
            {
                Console.WriteLine("No allocation was found for this project and employee. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var releasedCount = 0;
            foreach (var allocation in allocations)
            {
                if (_allocationRepository.Delete(allocation.Id))
                {
                    releasedCount++;
                    _notificationService.NotifyReleased(employee, project, allocation);
                }
            }

            Console.WriteLine(releasedCount == allocations.Count
                ? "Employee allocation released. Press Enter to continue..."
                : "Some allocations could not be released. Press Enter to continue...");
            Console.ReadLine();
        }

        public void ListProjects()
        {
            Console.WriteLine("-- Client Projects --");
            var projects = _projectRepository.GetAll().ToList();

            if (projects.Count == 0)
            {
                Console.WriteLine("No projects found.");
            }
            else
            {
                foreach (var project in projects)
                {
                    var skills = project.RequiredSkills.Count == 0
                        ? "None"
                        : string.Join(", ", project.RequiredSkills.Select(skill =>
                            $"{skill.Name} ({skill.MinimumProficiencyLevel}/5)"));

                    Console.WriteLine($"Id: {project.Id}, Name: {project.Name}, Client: {project.Client}, " +
                        $"Dates: {project.StartDate:yyyy-MM-dd} to {project.EndDate:yyyy-MM-dd}, " +
                        $"Status: {project.Status}, Required skills: {skills}");
                }
            }

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        public void AllocateEmployee()
        {
            Console.WriteLine("-- Allocate Employee --");
            var projectId = ReadInteger("Project Id: ");
            var employeeId = ReadInteger("Employee Id: ");
            var startDate = ReadDate("Allocation start date (yyyy-MM-dd): ");
            var endDate = ReadDate("Allocation end date (yyyy-MM-dd): ");
            var percentage = ReadPercentage();

            var allocation = new ProjectAllocation
            {
                ProjectId = projectId,
                EmployeeId = employeeId,
                StartDate = startDate,
                EndDate = endDate,
                AllocationPercentage = percentage
            };

            if (TryAllocateEmployee(allocation, out var error))
                Console.WriteLine("Employee allocated successfully. Press Enter to continue...");
            else
                Console.WriteLine($"Allocation failed: {error} Press Enter to continue...");

            Console.ReadLine();
        }

        public bool TryAllocateEmployee(ProjectAllocation allocation, out string error)
        {
            error = string.Empty;

            if (allocation.StartDate > allocation.EndDate)
            {
                error = "allocation end date must be on or after the start date.";
                return false;
            }

            if (allocation.AllocationPercentage is < 1 or > 100)
            {
                error = "allocation percentage must be between 1 and 100.";
                return false;
            }

            var project = _projectRepository.GetById(allocation.ProjectId);
            if (project == null)
            {
                error = "project was not found.";
                return false;
            }

            if (allocation.StartDate < project.StartDate || allocation.EndDate > project.EndDate)
            {
                error = "allocation dates must fall within the project dates.";
                return false;
            }

            var employee = _employeeRepository.GetById(allocation.EmployeeId);
            if (employee == null)
            {
                error = "employee was not found.";
                return false;
            }

            if (employee.Status == EmployeeStatus.Inactive)
            {
                error = "inactive employees cannot be allocated.";
                return false;
            }

            var overlappingAllocation = _allocationRepository.GetAll()
                .Where(existing => existing.EmployeeId == allocation.EmployeeId
                    && RangesOverlap(existing.StartDate, existing.EndDate, allocation.StartDate, allocation.EndDate))
                .Sum(existing => existing.AllocationPercentage);

            if (overlappingAllocation + allocation.AllocationPercentage > 100)
            {
                error = $"employee already has {overlappingAllocation}% allocated during the overlapping period.";
                return false;
            }

            _allocationRepository.Add(allocation);
            _notificationService.NotifyAllocated(employee, project, allocation);
            return true;
        }

        public void SuggestAvailableEmployees()
        {
            Console.WriteLine("-- Suggest Available Employees --");
            var projectId = ReadInteger("Project Id: ");
            var project = _projectRepository.GetById(projectId);
            if (project == null)
            {
                Console.WriteLine("Project was not found. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var suggestions = GetAvailableEmployees(projectId).ToList();
            if (suggestions.Count == 0)
            {
                Console.WriteLine("No active employees with matching skills and available capacity were found.");
            }
            else
            {
                foreach (var employee in suggestions)
                    Console.WriteLine($"Id: {employee.Id}, Name: {employee.Name}, Skills: {FormatSkills(employee)}");
            }

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        public IEnumerable<EmployeeModel> GetAvailableEmployees(int projectId)
        {
            var project = _projectRepository.GetById(projectId);
            if (project == null)
                return Enumerable.Empty<EmployeeModel>();

            return _employeeRepository.GetAll()
                .Where(employee => employee.Status == EmployeeStatus.Active)
                .Where(employee => HasRequiredSkills(employee, project))
                .Where(employee => GetOverlappingAllocation(employee.Id, project.StartDate, project.EndDate) < 100)
                .ToList();
        }

        private int GetOverlappingAllocation(int employeeId, DateTime startDate, DateTime endDate)
        {
            return _allocationRepository.GetAll()
                .Where(allocation => allocation.EmployeeId == employeeId
                    && RangesOverlap(allocation.StartDate, allocation.EndDate, startDate, endDate))
                .Sum(allocation => allocation.AllocationPercentage);
        }

        private static bool HasRequiredSkills(EmployeeModel employee, ClientProject project)
        {
            return project.RequiredSkills.All(required => employee.Skills.Any(skill =>
                string.Equals(skill.Name.Trim(), required.Name.Trim(), StringComparison.OrdinalIgnoreCase)
                && skill.ProficiencyLevel >= required.MinimumProficiencyLevel));
        }

        private static bool RangesOverlap(DateTime firstStart, DateTime firstEnd, DateTime secondStart, DateTime secondEnd)
        {
            return firstStart <= secondEnd && secondStart <= firstEnd;
        }

        private static List<ProjectSkill> ReadRequiredSkills()
        {
            var skills = new List<ProjectSkill>();
            var count = ReadInteger("Number of required skills: ");
            while (count < 1)
            {
                Console.WriteLine("At least one required skill is needed.");
                count = ReadInteger("Number of required skills: ");
            }

            for (var index = 1; index <= count; index++)
            {
                Console.Write($"Required skill {index} name: ");
                var name = Console.ReadLine()?.Trim() ?? string.Empty;
                while (string.IsNullOrWhiteSpace(name))
                {
                    Console.Write("Skill name is required: ");
                    name = Console.ReadLine()?.Trim() ?? string.Empty;
                }

                var proficiency = ReadProficiency($"Minimum proficiency for {name} (1-5): ");
                skills.Add(new ProjectSkill { Name = name, MinimumProficiencyLevel = proficiency });
            }

            return skills;
        }

        private static DateTime ReadDate(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (DateTime.TryParseExact(Console.ReadLine(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return date.Date;
                Console.WriteLine("Enter a valid date using yyyy-MM-dd.");
            }
        }

        private static int ReadInteger(string prompt)
        {
            while (true)
            {
                Console.Write(prompt);
                if (int.TryParse(Console.ReadLine(), out var value))
                    return value;
                Console.WriteLine("Enter a valid whole number.");
            }
        }

        private static int ReadPercentage()
        {
            while (true)
            {
                var percentage = ReadInteger("Allocation percentage (1-100): ");
                if (percentage is >= 1 and <= 100)
                    return percentage;
                Console.WriteLine("Allocation percentage must be between 1 and 100.");
            }
        }

        private static int ReadProficiency(string prompt)
        {
            while (true)
            {
                var proficiency = ReadInteger(prompt);
                if (proficiency is >= 1 and <= 5)
                    return proficiency;
                Console.WriteLine("Proficiency must be between 1 and 5.");
            }
        }

        private static string FormatSkills(EmployeeModel employee)
        {
            return string.Join(", ", employee.Skills.Select(skill => $"{skill.Name} ({skill.ProficiencyLevel}/5)"));
        }
    }
}
