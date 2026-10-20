using System.Text.Json.Serialization;

namespace ResourceManagementSystem.Models
{
    public class ProjectAllocation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("projectId")]
        public int ProjectId { get; set; }

        [JsonPropertyName("employeeId")]
        public int EmployeeId { get; set; }

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("allocationPercentage")]
        public int AllocationPercentage { get; set; }
    }
}
