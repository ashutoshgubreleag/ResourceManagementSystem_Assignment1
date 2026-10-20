using System;
using System.Collections.Generic;
using System.Text;

namespace ResourceManagementSystem.Services.Employee
{
    using System;
    using System.Globalization;
    using ResourceManagementSystem.Models;

    public class EmployeeService : IEmployeeService
    {
        private readonly ResourceManagementSystem.Repositories.IBaseEntityRepository<Employee> _repo;

        public EmployeeService(ResourceManagementSystem.Repositories.IBaseEntityRepository<Employee> repo)
        {
            _repo = repo;
        }

        public void AddEmployee()
        {
            Console.WriteLine("-- Add Employee --");
            Console.Write("Name: ");
            var name = Console.ReadLine() ?? string.Empty;
            Console.Write("Email: ");
            var email = Console.ReadLine() ?? string.Empty;
            Console.Write("Position: ");
            var position = Console.ReadLine() ?? string.Empty;
            Console.Write("Department: ");
            var department = Console.ReadLine() ?? string.Empty;
            Console.Write("Salary: ");
            var salaryText = Console.ReadLine() ?? string.Empty;
            var skills = ReadSkills();
            decimal salary = 0;
            if (!decimal.TryParse(salaryText, NumberStyles.Number, CultureInfo.InvariantCulture, out salary))
            {
                decimal.TryParse(salaryText, out salary);
            }

            var emp = new Employee
            {
                Name = name,
                Email = email,
                Position = position,
                Department = department,
                Salary = salary,
                Status = EmployeeStatus.Active,
                Skills = skills
            };

            var added = _repo.Add(emp);
            Console.WriteLine($"Added employee with Id {added.Id}. Press Enter to continue...");
            Console.ReadLine();
        }

        public void UpdateEmployee()
        {
            Console.WriteLine("-- Update Employee --");
            Console.Write("Employee Id: ");
            var idText = Console.ReadLine() ?? string.Empty;
            if (!int.TryParse(idText, out var id))
            {
                Console.WriteLine("Invalid id. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var existing = _repo.GetById(id);
            // Patch placeholder - no functional change
            if (existing == null)
            {
                Console.WriteLine("Employee not found. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"Leave blank to keep existing value.");
            Console.Write($"Name ({existing.Name}): ");
            var name = Console.ReadLine();
            Console.Write($"Email ({existing.Email}): ");
            var email = Console.ReadLine();
            Console.Write($"Position ({existing.Position}): ");
            var position = Console.ReadLine();
            Console.Write($"Department ({existing.Department}): ");
            var department = Console.ReadLine();
            Console.Write($"Salary ({existing.Salary}): ");
            var salaryText = Console.ReadLine();
            Console.Write("Update skills? (y/N): ");
            var updateSkills = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(name)) existing.Name = name;
            if (!string.IsNullOrWhiteSpace(email)) existing.Email = email;
            if (!string.IsNullOrWhiteSpace(position)) existing.Position = position;
            if (!string.IsNullOrWhiteSpace(department)) existing.Department = department;
            if (!string.IsNullOrWhiteSpace(salaryText))
            {
                if (decimal.TryParse(salaryText, NumberStyles.Number, CultureInfo.InvariantCulture, out var s))
                    existing.Salary = s;
                else if (decimal.TryParse(salaryText, out s))
                    existing.Salary = s;
            }

            if (updateSkills?.Equals("y", StringComparison.OrdinalIgnoreCase) == true)
                existing.Skills = ReadSkills();

            var ok = _repo.Update(existing);
            Console.WriteLine(ok ? "Employee updated. Press Enter to continue..." : "Update failed. Press Enter to continue...");
            Console.ReadLine();
        }

        public void DeactivateEmployee()
        {
            Console.WriteLine("-- Deactivate Employee --");
            Console.Write("Employee Id: ");
            var idText = Console.ReadLine() ?? string.Empty;
            if (!int.TryParse(idText, out var id))
            {
                Console.WriteLine("Invalid id. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            var existing = _repo.GetById(id);
            if (existing == null)
            {
                Console.WriteLine("Employee not found. Press Enter to continue...");
                Console.ReadLine();
                return;
            }

            existing.Status = EmployeeStatus.Inactive;
            var ok = _repo.Update(existing);
            Console.WriteLine(ok ? "Employee deactivated. Press Enter to continue..." : "Deactivate failed. Press Enter to continue...");
            Console.ReadLine();
        }

        public void ListEmployees()
        {
            Console.WriteLine("-- Employees --");
            var list = _repo.GetAll();
            foreach (var e in list)
            {
                Console.WriteLine($"Id: {e.Id}, Name: {e.Name}, Email: {e.Email}, Position: {e.Position}, Department: {e.Department}, Salary: {e.Salary}, Status: {e.Status}");
                Console.WriteLine($"  Skills: {FormatSkills(e.Skills)}");
            }
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        private static List<EmployeeSkill> ReadSkills()
        {
            var skills = new List<EmployeeSkill>();
            Console.Write("Number of skills: ");
            if (!int.TryParse(Console.ReadLine(), out var count) || count < 1)
            {
                Console.WriteLine("At least one skill is required.");
                return ReadSkills();
            }

            for (var index = 1; index <= count; index++)
            {
                Console.Write($"Skill {index} name: ");
                var name = Console.ReadLine()?.Trim() ?? string.Empty;
                while (string.IsNullOrWhiteSpace(name))
                {
                    Console.Write("Skill name is required. Enter a name: ");
                    name = Console.ReadLine()?.Trim() ?? string.Empty;
                }

                var proficiency = ReadProficiency();
                skills.Add(new EmployeeSkill { Name = name, ProficiencyLevel = proficiency });
            }

            return skills;
        }

        private static int ReadProficiency()
        {
            while (true)
            {
                Console.Write("Proficiency level (1-5): ");
                if (int.TryParse(Console.ReadLine(), out var proficiency) && proficiency is >= 1 and <= 5)
                    return proficiency;

                Console.WriteLine("Proficiency must be a whole number from 1 to 5.");
            }
        }

        private static string FormatSkills(IEnumerable<EmployeeSkill> skills)
        {
            var skillList = skills?.ToList() ?? new List<EmployeeSkill>();
            return skillList.Count == 0
                ? "None"
                : string.Join(", ", skillList.Select(skill => $"{skill.Name} ({skill.ProficiencyLevel}/5)"));
        }
    }
}
