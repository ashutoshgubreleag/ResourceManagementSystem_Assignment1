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
                Status = EmployeeStatus.Active
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
            }
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }
    }
}
