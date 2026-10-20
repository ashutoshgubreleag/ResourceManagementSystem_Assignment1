using Microsoft.EntityFrameworkCore;
using ResourceManagementSystem.Models;

namespace ResourceManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<ClientProject> Projects { get; set; } = null!;
        public DbSet<ProjectAllocation> ProjectAllocations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>()
                .OwnsMany(employee => employee.Skills, skillBuilder =>
                {
                    skillBuilder.WithOwner().HasForeignKey("EmployeeId");
                    skillBuilder.Property<int>("Id");
                    skillBuilder.HasKey("Id");
                    skillBuilder.Property(skill => skill.Name).IsRequired();
                    skillBuilder.Property(skill => skill.ProficiencyLevel).IsRequired();
                });

            modelBuilder.Entity<ClientProject>()
                .OwnsMany(project => project.RequiredSkills, skillBuilder =>
                {
                    skillBuilder.WithOwner().HasForeignKey("ProjectId");
                    skillBuilder.Property<int>("Id");
                    skillBuilder.HasKey("Id");
                    skillBuilder.Property(skill => skill.Name).IsRequired();
                    skillBuilder.Property(skill => skill.MinimumProficiencyLevel).IsRequired();
                });

            modelBuilder.Entity<ProjectAllocation>()
                .HasKey(allocation => allocation.Id);
        }
    }
}
