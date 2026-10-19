using System.Collections.Generic;

namespace ResourceManagementSystem.Repositories
{
    public interface IBaseEntityRepository<TEntity> where TEntity : class
    {
        TEntity Add(TEntity entity);
        bool Update(TEntity entity);
        TEntity? GetById(object id);
        IEnumerable<TEntity> GetAll();
        bool Delete(object id);
    }
}
