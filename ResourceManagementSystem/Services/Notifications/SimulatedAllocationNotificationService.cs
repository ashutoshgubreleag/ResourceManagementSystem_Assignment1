using ResourceManagementSystem.Models;
using EmployeeModel = ResourceManagementSystem.Models.Employee;

namespace ResourceManagementSystem.Services.Notifications
{
    public class SimulatedAllocationNotificationService : IAllocationNotificationService
    {
        public void NotifyAllocated(EmployeeModel employee, ClientProject project, ProjectAllocation allocation)
        {
            Console.WriteLine($"[Notification] {employee.Name} was allocated to project '{project.Name}' " +
                $"from {allocation.StartDate:yyyy-MM-dd} to {allocation.EndDate:yyyy-MM-dd} " +
                $"at {allocation.AllocationPercentage}% allocation.");
        }

        public void NotifyReleased(EmployeeModel employee, ClientProject project, ProjectAllocation allocation)
        {
            Console.WriteLine($"[Notification] {employee.Name} was released from project '{project.Name}' " +
                $"for the allocation period {allocation.StartDate:yyyy-MM-dd} to {allocation.EndDate:yyyy-MM-dd}.");
        }
    }
}