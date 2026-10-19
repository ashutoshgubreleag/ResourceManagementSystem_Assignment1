using System;
using System.Collections.Generic;
using System.Text;

namespace ResourceManagementSystem.Services.Employee
{
    public interface IEmployeeService
    {
        void AddEmployee();
        void UpdateEmployee();
        void DeactivateEmployee();
        void ListEmployees();
    }
}
