using System.Collections.Generic;
using ResourceManagementSystem.Models;
using EmployeeModel = ResourceManagementSystem.Models.Employee;

namespace ResourceManagementSystem.Services.Project
{
    public interface IProjectService
    {
        void CreateProject();
        void ListProjects();
        void AllocateEmployee();
        void SuggestAvailableEmployees();
        bool TryAllocateEmployee(ProjectAllocation allocation, out string error);
        IEnumerable<EmployeeModel> GetAvailableEmployees(int projectId);
    }
}
