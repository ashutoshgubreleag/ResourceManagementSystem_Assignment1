using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ResourceManagementSystem.Data;

namespace ResourceManagementSystem.Repositories
{
    public class BaseEntityRepository<TEntity> : IBaseEntityRepository<TEntity> where TEntity : class
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public BaseEntityRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = _context.Set<TEntity>();
        }

        public TEntity Add(TEntity entity)
        {
            _dbSet.Add(entity);
            _context.SaveChanges();
            return entity;
        }

        public bool Update(TEntity entity)
        {
            _dbSet.Update(entity);
            return _context.SaveChanges() > 0;
        }

        public TEntity? GetById(object id)
        {
            return _dbSet.Find(id);
        }

        public IEnumerable<TEntity> GetAll()
        {
            return _dbSet.AsNoTracking().ToList();
        }

        public bool Delete(object id)
        {
            var entity = _dbSet.Find(id);
            if (entity == null) return false;
            _dbSet.Remove(entity);
            return _context.SaveChanges() > 0;
        }
    }
}
