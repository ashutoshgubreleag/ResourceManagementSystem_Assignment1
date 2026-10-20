using ResourceManagementSystem.Models;
using ResourceManagementSystem.Repositories;
using ResourceManagementSystem.Services.Notifications;
using ResourceManagementSystem.Services.Project;
using EmployeeModel = ResourceManagementSystem.Models.Employee;
using Xunit;

namespace ResourceManagementSystem.Tests;

public class ProjectServiceTests
{
    [Fact]
    public void TryAllocateEmployee_RejectsInactiveEmployee()
    {
        var employee = new EmployeeModel { Id = 1, Name = "Inactive", Status = EmployeeStatus.Inactive };
        var project = CreateProject();
        var service = CreateService(new[] { employee }, new[] { project });

        var result = service.TryAllocateEmployee(CreateAllocation(), out var error);

        Assert.False(result);
        Assert.Equal("inactive employees cannot be allocated.", error);
    }

    [Fact]
    public void TryAllocateEmployee_RejectsAllocationOutsideProjectDates()
    {
        var employee = new EmployeeModel { Id = 1, Name = "Active", Status = EmployeeStatus.Active };
        var project = CreateProject();
        var service = CreateService(new[] { employee }, new[] { project });
        var allocation = CreateAllocation(startDate: new DateTime(2026, 3, 31));

        var result = service.TryAllocateEmployee(allocation, out var error);

        Assert.False(result);
        Assert.Equal("allocation dates must fall within the project dates.", error);
    }

    [Fact]
    public void TryAllocateEmployee_RejectsOverlappingAllocationAbove100Percent()
    {
        var employee = new EmployeeModel { Id = 1, Name = "Active", Status = EmployeeStatus.Active };
        var project = CreateProject();
        var existing = CreateAllocation(id: 10, percentage: 75);
        var service = CreateService(new[] { employee }, new[] { project }, new[] { existing });

        var result = service.TryAllocateEmployee(CreateAllocation(percentage: 30), out var error);

        Assert.False(result);
        Assert.Contains("employee already has 75% allocated", error);
    }

    [Fact]
    public void TryAllocateEmployee_AddsValidAllocation()
    {
        var employee = new EmployeeModel { Id = 1, Name = "Active", Status = EmployeeStatus.Active };
        var project = CreateProject();
        var allocationRepository = new InMemoryRepository<ProjectAllocation>();
        var service = CreateService(new[] { employee }, new[] { project }, repository: allocationRepository);

        var result = service.TryAllocateEmployee(CreateAllocation(percentage: 50), out var error);

        Assert.True(result);
        Assert.Empty(error);
        Assert.Single(allocationRepository.GetAll());
        Assert.Equal(50, allocationRepository.GetAll().Single().AllocationPercentage);
    }

    [Fact]
    public void GetAvailableEmployees_ReturnsOnlyActiveEmployeesWithMatchingSkillsAndCapacity()
    {
        var project = CreateProject(
            requiredSkills: new[]
            {
                new ProjectSkill { Name = "C#", MinimumProficiencyLevel = 4 },
                new ProjectSkill { Name = "SQL", MinimumProficiencyLevel = 3 }
            });
        var matching = new EmployeeModel
        {
            Id = 1,
            Name = "Matching",
            Status = EmployeeStatus.Active,
            Skills = new List<EmployeeSkill>
            {
                new() { Name = "C#", ProficiencyLevel = 4 },
                new() { Name = "SQL", ProficiencyLevel = 3 }
            }
        };
        var insufficient = new EmployeeModel
        {
            Id = 2,
            Name = "Insufficient",
            Status = EmployeeStatus.Active,
            Skills = new List<EmployeeSkill>
            {
                new() { Name = "C#", ProficiencyLevel = 3 },
                new() { Name = "SQL", ProficiencyLevel = 3 }
            }
        };
        var inactive = new EmployeeModel
        {
            Id = 3,
            Name = "Inactive",
            Status = EmployeeStatus.Inactive,
            Skills = matching.Skills
        };
        var unavailable = new EmployeeModel
        {
            Id = 4,
            Name = "Unavailable",
            Status = EmployeeStatus.Active,
            Skills = matching.Skills
        };
        var unavailableAllocation = CreateAllocation(id: 20, employeeId: 4, percentage: 100);

        var service = CreateService(
            new[] { matching, insufficient, inactive, unavailable },
            new[] { project },
            new[] { unavailableAllocation });

        var result = service.GetAvailableEmployees(project.Id).ToList();

        var employee = Assert.Single(result);
        Assert.Equal(matching.Id, employee.Id);
    }

    private static ProjectService CreateService(
        IEnumerable<EmployeeModel> employees,
        IEnumerable<ClientProject> projects,
        IEnumerable<ProjectAllocation>? allocations = null,
        InMemoryRepository<ProjectAllocation>? repository = null)
    {
        return new ProjectService(
            new InMemoryRepository<ClientProject>(projects),
            new InMemoryRepository<EmployeeModel>(employees),
            repository ?? new InMemoryRepository<ProjectAllocation>(allocations ?? Enumerable.Empty<ProjectAllocation>()),
            new NoOpAllocationNotificationService());
    }

    private static ClientProject CreateProject(IEnumerable<ProjectSkill>? requiredSkills = null)
    {
        return new ClientProject
        {
            Id = 1,
            Name = "Test Project",
            Client = "Test Client",
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2026, 6, 30),
            RequiredSkills = requiredSkills?.ToList() ?? new List<ProjectSkill>()
        };
    }

    private static ProjectAllocation CreateAllocation(
        int id = 0,
        int projectId = 1,
        int employeeId = 1,
        int percentage = 25,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        return new ProjectAllocation
        {
            Id = id,
            ProjectId = projectId,
            EmployeeId = employeeId,
            StartDate = startDate ?? new DateTime(2026, 4, 1),
            EndDate = endDate ?? new DateTime(2026, 6, 30),
            AllocationPercentage = percentage
        };
    }

    private sealed class NoOpAllocationNotificationService : IAllocationNotificationService
    {
        public void NotifyAllocated(EmployeeModel employee, ClientProject project, ProjectAllocation allocation)
        {
        }

        public void NotifyReleased(EmployeeModel employee, ClientProject project, ProjectAllocation allocation)
        {
        }
    }

    private sealed class InMemoryRepository<TEntity> : IBaseEntityRepository<TEntity>
        where TEntity : class
    {
        private readonly List<TEntity> _entities;

        public InMemoryRepository(IEnumerable<TEntity>? entities = null)
        {
            _entities = entities?.ToList() ?? new List<TEntity>();
        }

        public TEntity Add(TEntity entity)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty?.PropertyType == typeof(int) && (int)(idProperty.GetValue(entity) ?? 0) == 0)
            {
                var nextId = _entities
                    .Select(item => (int)(idProperty.GetValue(item) ?? 0))
                    .DefaultIfEmpty(0)
                    .Max() + 1;
                idProperty.SetValue(entity, nextId);
            }

            _entities.Add(entity);
            return entity;
        }

        public bool Update(TEntity entity)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty == null) return false;
            var id = idProperty.GetValue(entity);
            var index = _entities.FindIndex(item => Equals(idProperty.GetValue(item), id));
            if (index < 0) return false;
            _entities[index] = entity;
            return true;
        }

        public TEntity? GetById(object id)
        {
            var idProperty = typeof(TEntity).GetProperty("Id");
            return _entities.FirstOrDefault(item => Equals(idProperty?.GetValue(item), id));
        }

        public IEnumerable<TEntity> GetAll() => _entities;

        public bool Delete(object id)
        {
            var entity = GetById(id);
            return entity != null && _entities.Remove(entity);
        }
    }
}
