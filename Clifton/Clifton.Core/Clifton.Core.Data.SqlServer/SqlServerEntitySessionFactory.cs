using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Clifton.Core.Data.Abstractions;

using Microsoft.EntityFrameworkCore;

namespace Clifton.Core.Data.SqlServer
{
    public sealed class SqlServerEntitySessionFactory : IEntitySessionFactory
    {
        private readonly string connectionString;
        private readonly Type[] entityTypes;
        private readonly Action<ModelBuilder> configureModel;

        public SqlServerEntitySessionFactory(SqlServerEntitySessionFactoryOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            connectionString = options.ConnectionString ?? throw new ArgumentNullException(nameof(options.ConnectionString));
            entityTypes = (options.EntityTypes ?? Array.Empty<Type>()).Distinct().ToArray();
            configureModel = options.ConfigureModel;

            if (entityTypes.Length == 0)
            {
                throw new ArgumentException("At least one entity type must be registered.", nameof(options.EntityTypes));
            }

            foreach (Type entityType in entityTypes)
            {
                if (!typeof(IEntity).IsAssignableFrom(entityType))
                {
                    throw new ArgumentException("Entity type " + entityType.FullName + " does not implement IEntity.", nameof(options.EntityTypes));
                }
            }
        }

        public IEntitySession CreateSession()
        {
            return new SqlServerEntitySession(connectionString, entityTypes, configureModel);
        }

        internal DynamicSqlServerDbContext CreateDbContext()
        {
            DbContextOptionsBuilder<DynamicSqlServerDbContext> builder = new DbContextOptionsBuilder<DynamicSqlServerDbContext>();
            builder.UseSqlServer(connectionString);

            return new DynamicSqlServerDbContext(builder.Options, entityTypes, configureModel);
        }
    }

    internal sealed class SqlServerEntitySession : IEntitySession
    {
        private static readonly MethodInfo SetMethodDefinition = typeof(DbContext).GetMethods().
            Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethodDefinition && method.GetParameters().Length == 0);
        private static readonly MethodInfo InsertGenericMethodDefinition = typeof(SqlServerEntitySession).GetMethods(BindingFlags.Instance | BindingFlags.Public).
            Single(method => method.Name == nameof(Insert) && method.IsGenericMethodDefinition);
        private static readonly MethodInfo UpdateGenericMethodDefinition = typeof(SqlServerEntitySession).GetMethods(BindingFlags.Instance | BindingFlags.Public).
            Single(method => method.Name == nameof(Update) && method.IsGenericMethodDefinition);
        private static readonly MethodInfo DeleteGenericMethodDefinition = typeof(SqlServerEntitySession).GetMethods(BindingFlags.Instance | BindingFlags.Public).
            Single(method => method.Name == nameof(Delete) && method.IsGenericMethodDefinition && method.GetParameters()[0].ParameterType.IsGenericParameter);

        private readonly SqlServerEntitySessionFactory factory;
        private readonly HashSet<Type> mappedTypes;

        public SqlServerEntitySession(string connectionString, Type[] entityTypes, Action<ModelBuilder> configureModel)
        {
            factory = new SqlServerEntitySessionFactory(new SqlServerEntitySessionFactoryOptions()
            {
                ConnectionString = connectionString,
                EntityTypes = entityTypes,
                ConfigureModel = configureModel,
            });
            mappedTypes = entityTypes.ToHashSet();
        }

        public void Dispose()
        {
        }

        public List<T> Query<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            IQueryable<T> query = context.Set<T>().AsNoTracking();

            if (whereClause != null)
            {
                query = query.Where(whereClause);
            }

            return query.ToList();
        }

        public List<IEntity> Query(Type entityType)
        {
            EnsureMapped(entityType);

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return GetQueryable(context, entityType).Cast<IEntity>().ToList();
        }

        public List<IEntity> Query(Type entityType, Func<IEntity, bool> whereClause)
        {
            List<IEntity> records = Query(entityType);

            return whereClause == null ? records : records.Where(whereClause).ToList();
        }

        public List<T> SqlQuery<T>(string sql, params object[] parameters) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            IQueryable<T> query = context.Set<T>().FromSqlRaw(sql, parameters ?? Array.Empty<object>()).AsNoTracking();

            return query.ToList();
        }

        public T Single<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            List<T> records = Query(whereClause);

            if (records.Count == 0)
            {
                throw new ApplicationException("No rows were returned querying Single for " + typeof(T).Name);
            }

            if (records.Count > 1)
            {
                throw new ApplicationException("More than one row was returned querying Single for " + typeof(T).Name);
            }

            return records[0];
        }

        public T SingleOrDefault<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            List<T> records = Query(whereClause);

            if (records.Count > 1)
            {
                throw new ApplicationException("More than one row was returned querying SingleOrDefault for " + typeof(T).Name);
            }

            return records.SingleOrDefault();
        }

        public T FirstOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            IQueryable<T> query = context.Set<T>().AsNoTracking().OrderBy(orderBy);

            if (whereClause != null)
            {
                query = query.Where(whereClause);
            }

            return query.FirstOrDefault();
        }

        public T LastOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            IQueryable<T> query = context.Set<T>().AsNoTracking().OrderByDescending(orderBy);

            if (whereClause != null)
            {
                query = query.Where(whereClause);
            }

            return query.FirstOrDefault();
        }

        public int Count<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            IQueryable<T> query = context.Set<T>().AsNoTracking();

            return whereClause == null ? query.Count() : query.Count(whereClause);
        }

        public bool Exists<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();

            return context.Set<T>().AsNoTracking().Any(whereClause);
        }

        public int Insert<T>(T entity) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            ApplyInsertTimestamps(entity);
            context.Set<T>().Add(entity);
            context.SaveChanges();

            return entity.Id ?? 0;
        }

        public int Insert(IEntity entity)
        {
            return (int)InsertGenericMethodDefinition.MakeGenericMethod(entity.GetType()).Invoke(this, new object[] { entity });
        }

        public void InsertRange<T>(IEnumerable<T> entities) where T : class, IEntity
        {
            EnsureMapped(typeof(T));
            List<T> materialized = entities.ToList();

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            materialized.ForEach(ApplyInsertTimestamps);
            context.Set<T>().AddRange(materialized);
            context.SaveChanges();
        }

        public void Update<T>(T entity) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            T existing = context.Set<T>().Find(entity.Id);

            if (existing == null)
            {
                throw new ApplicationException("No row was found to update for " + typeof(T).Name + " with id " + entity.Id);
            }

            CopyPersistedProperties(entity, existing);
            ApplyUpdateTimestamps(entity, existing);
            context.SaveChanges();
        }

        public void Update(IEntity entity)
        {
            UpdateGenericMethodDefinition.MakeGenericMethod(entity.GetType()).Invoke(this, new object[] { entity });
        }

        public void Delete<T>(T entity) where T : class, IEntity
        {
            EnsureMapped(typeof(T));

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            T existing = context.Set<T>().Find(entity.Id);

            if (existing == null)
            {
                return;
            }

            context.Set<T>().Remove(existing);
            context.SaveChanges();
        }

        public void Delete(IEntity entity)
        {
            DeleteGenericMethodDefinition.MakeGenericMethod(entity.GetType()).Invoke(this, new object[] { entity });
        }

        public void Delete<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity
        {
            EnsureMapped(typeof(T));
            ArgumentNullException.ThrowIfNull(whereClause);

            using DynamicSqlServerDbContext context = factory.CreateDbContext();
            List<T> entities = context.Set<T>().Where(whereClause).ToList();

            if (entities.Count == 0)
            {
                return;
            }

            context.Set<T>().RemoveRange(entities);
            context.SaveChanges();
        }

        private void EnsureMapped(Type entityType)
        {
            if (!mappedTypes.Contains(entityType))
            {
                throw new InvalidOperationException("Entity type " + entityType.FullName + " is not registered with the session factory.");
            }
        }

        private static IQueryable GetQueryable(DbContext context, Type entityType)
        {
            return (IQueryable)SetMethodDefinition.MakeGenericMethod(entityType).Invoke(context, Array.Empty<object>());
        }

        private static void ApplyInsertTimestamps(IEntity entity)
        {
            if (entity is not ICreateUpdate createUpdate)
            {
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            createUpdate.CreatedOn = utcNow;
            createUpdate.UpdatedOn = utcNow;
        }

        private static void ApplyUpdateTimestamps(IEntity source, IEntity destination)
        {
            if (source is ICreateUpdate sourceCreateUpdate)
            {
                DateTime utcNow = DateTime.UtcNow;
                sourceCreateUpdate.UpdatedOn = utcNow;

                if (destination is ICreateUpdate destinationCreateUpdate)
                {
                    destinationCreateUpdate.UpdatedOn = utcNow;
                }
            }
        }

        private static void CopyPersistedProperties(IEntity source, IEntity destination)
        {
            foreach (PropertyInfo property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite || IsKeyProperty(property) || !IsPersistedProperty(property))
                {
                    continue;
                }

                property.SetValue(destination, property.GetValue(source));
            }
        }

        private static bool IsPersistedProperty(PropertyInfo property)
        {
            return Attribute.IsDefined(property, typeof(ColumnAttribute)) ||
                   Attribute.IsDefined(property, typeof(KeyAttribute)) ||
                   property.Name == nameof(IEntity.Id);
        }

        private static bool IsKeyProperty(PropertyInfo property)
        {
            return Attribute.IsDefined(property, typeof(KeyAttribute)) || property.Name == nameof(IEntity.Id);
        }
    }

    internal sealed class DynamicSqlServerDbContext : DbContext
    {
        private readonly IReadOnlyCollection<Type> entityTypes;
        private readonly Action<ModelBuilder> configureModel;

        public DynamicSqlServerDbContext(DbContextOptions<DynamicSqlServerDbContext> options, IReadOnlyCollection<Type> entityTypes, Action<ModelBuilder> configureModel)
            : base(options)
        {
            this.entityTypes = entityTypes;
            this.configureModel = configureModel;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (Type entityType in entityTypes)
            {
                modelBuilder.Entity(entityType);
            }

            configureModel?.Invoke(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
    }
}
