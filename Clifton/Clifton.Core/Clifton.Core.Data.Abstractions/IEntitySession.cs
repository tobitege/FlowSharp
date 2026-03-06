using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Clifton.Core.Data.Abstractions
{
    public interface IEntitySession : IDisposable
    {
        List<T> Query<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        List<IEntity> Query(Type entityType);
        List<IEntity> Query(Type entityType, Func<IEntity, bool> whereClause);
        List<T> SqlQuery<T>(string sql, params object[] parameters) where T : class, IEntity;
        T Single<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        T SingleOrDefault<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        T FirstOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        T LastOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        int Count<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity;
        bool Exists<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity;
        int Insert<T>(T entity) where T : class, IEntity;
        int Insert(IEntity entity);
        void InsertRange<T>(IEnumerable<T> entities) where T : class, IEntity;
        void Update<T>(T entity) where T : class, IEntity;
        void Update(IEntity entity);
        void Delete<T>(T entity) where T : class, IEntity;
        void Delete(IEntity entity);
        void Delete<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity;
    }
}
