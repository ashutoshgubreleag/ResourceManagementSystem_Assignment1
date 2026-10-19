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
    }
}
