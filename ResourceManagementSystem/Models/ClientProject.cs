using System.Text.Json.Serialization;

namespace ResourceManagementSystem.Models
{
    public enum ProjectStatus
    {
        Planned = 0,
        Active = 1,
        Completed = 2,
        Cancelled = 3
    }

    public class ProjectSkill
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("minimumProficiencyLevel")]
        public int MinimumProficiencyLevel { get; set; } = 1;
    }

    public class ClientProject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("client")]
        public string Client { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("status")]
        public ProjectStatus Status { get; set; } = ProjectStatus.Planned;

        [JsonPropertyName("requiredSkills")]
        public List<ProjectSkill> RequiredSkills { get; set; } = new();
    }
}
