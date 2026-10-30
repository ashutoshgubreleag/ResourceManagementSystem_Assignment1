using ApexPost.Database.Models;
using ApexPost.Repositories.Specifications;

namespace ApexPost.Repositories;

/// <summary>
/// Generic repository. The constraint <c>class, IEntity</c> guarantees every entity has an
/// integer key the store can assign and look up, so one implementation serves every type.
/// Reads return <see cref="ValueTask{TResult}"/> because after the first load they complete
/// synchronously from the in-memory snapshot (no Task allocation on the hot path).
/// Every method takes a <see cref="CancellationToken"/> so a cancelled HTTP request stops I/O.
/// </summary>
public interface IBaseEntityRepository<TEntity> where TEntity : class, IEntity
{
    ValueTask<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the entities that satisfy a specification (used heavily by reports).</summary>
    ValueTask<IReadOnlyList<TEntity>> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    ValueTask<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Inserts the entity; assigns the next Id when <c>Id == 0</c>.</summary>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Updates several entities in one atomic write (all or nothing).</summary>
    Task<bool> UpdateRangeAsync(IReadOnlyCollection<TEntity> entities, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
