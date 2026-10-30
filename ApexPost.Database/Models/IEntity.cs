namespace ApexPost.Database.Models;

/// <summary>
/// Every persisted record has an integer key. The repository assigns it on insert when it is 0,
/// which mirrors an identity column and keeps next week's database migration mechanical.
/// </summary>
public interface IEntity
{
    int Id { get; set; }
}
