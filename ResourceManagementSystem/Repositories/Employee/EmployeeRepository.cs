using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResourceManagementSystem.Models;

namespace ResourceManagementSystem.Repositories.Employee
{
    public class EmployeeRepository : IEmployeeRepository
    {
        private readonly object _fileLock = new object();
        private readonly string _filePath;

        public EmployeeRepository()
        {
            _filePath = FindEmployeeJsonPath() ?? "Employee.json";
        }

        private string? FindEmployeeJsonPath()
        {
            var dir = AppContext.BaseDirectory;
            var current = new DirectoryInfo(dir);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "Employee.json");
                if (File.Exists(candidate))
                    return candidate;
                current = current.Parent;
            }
            return null;
        }

        private List<ResourceManagementSystem.Models.Employee> ReadEmployees()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_filePath))
                    return new List<ResourceManagementSystem.Models.Employee>();

                var json = File.ReadAllText(_filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var wrapper = JsonSerializer.Deserialize<EmployeesWrapper>(json, options);
                return wrapper?.Employees ?? new List<ResourceManagementSystem.Models.Employee>();
            }
        }

        private void WriteEmployees(List<ResourceManagementSystem.Models.Employee> employees)
        {
            lock (_fileLock)
            {
                var wrapper = new EmployeesWrapper { Employees = employees };
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(wrapper, options);
                File.WriteAllText(_filePath, json);
            }
        }

        public IEnumerable<ResourceManagementSystem.Models.Employee> GetAll() => ReadEmployees();

        public ResourceManagementSystem.Models.Employee? GetById(int id) => ReadEmployees().FirstOrDefault(e => e.Id == id);

        public ResourceManagementSystem.Models.Employee Add(ResourceManagementSystem.Models.Employee employee)
        {
            var employees = ReadEmployees();
            var nextId = employees.Any() ? employees.Max(e => e.Id) + 1 : 1;
            employee.Id = nextId;
            employees.Add(employee);
            WriteEmployees(employees);
            return employee;
        }

        public bool Update(ResourceManagementSystem.Models.Employee employee)
        {
            var employees = ReadEmployees();
            var idx = employees.FindIndex(e => e.Id == employee.Id);
            if (idx < 0) return false;
            employees[idx] = employee;
            WriteEmployees(employees);
            return true;
        }
        public bool Deactivate(int id)
        {
            var employees = ReadEmployees();
            var idx = employees.FindIndex(e => e.Id == id);
            if (idx < 0) return false;
            employees[idx].Status = ResourceManagementSystem.Models.EmployeeStatus.Inactive;
            WriteEmployees(employees);
            return true;
        }

        private class EmployeesWrapper
        {
            [JsonPropertyName("employees")]
            public List<ResourceManagementSystem.Models.Employee> Employees { get; set; } = new List<ResourceManagementSystem.Models.Employee>();
        }
    }
}
