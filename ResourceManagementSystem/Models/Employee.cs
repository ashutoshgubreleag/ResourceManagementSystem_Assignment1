using System.Text.Json.Serialization;

namespace ResourceManagementSystem.Models
{
    public enum EmployeeStatus
    {
        Active = 0,
        Inactive = 1
    }

    public class Employee
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string Position { get; set; } = string.Empty;

        [JsonPropertyName("department")]
        public string Department { get; set; } = string.Empty;

        [JsonPropertyName("salary")]
        public decimal Salary { get; set; }

        [JsonPropertyName("status")]
        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    }
}
