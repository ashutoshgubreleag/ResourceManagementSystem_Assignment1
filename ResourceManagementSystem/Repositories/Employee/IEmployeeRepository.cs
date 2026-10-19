using System;
using System.Collections.Generic;
using ResourceManagementSystem.Models;
using System.Text;

namespace ResourceManagementSystem.Repositories.Employee
{

    public interface IEmployeeRepository
    {
        IEnumerable<ResourceManagementSystem.Models.Employee> GetAll();
        ResourceManagementSystem.Models.Employee? GetById(int id);
        ResourceManagementSystem.Models.Employee Add(ResourceManagementSystem.Models.Employee employee);
        bool Update(ResourceManagementSystem.Models.Employee employee);
        bool Deactivate(int id);
    }
}
