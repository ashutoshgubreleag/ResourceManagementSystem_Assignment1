using ResourceManagementSystem.Models;
using EmployeeModel = ResourceManagementSystem.Models.Employee;

namespace ResourceManagementSystem.Services.Notifications
{
    public interface IAllocationNotificationService
    {
        void NotifyAllocated(EmployeeModel employee, ClientProject project, ProjectAllocation allocation);
        void NotifyReleased(EmployeeModel employee, ClientProject project, ProjectAllocation allocation);
    }
}
