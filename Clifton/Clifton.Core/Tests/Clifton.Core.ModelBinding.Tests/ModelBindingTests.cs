using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

using Clifton.Core.Data.Abstractions;
using Clifton.Core.ModelBinding;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Clifton.Core.ModelBinding.Tests
{
    [TestClass]
    public class ModelBindingTests
    {
        [TestMethod]
        public void CreateViewAndLoadRecords_LoadsRowsAndMappedCollection()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            store.Seed(new TestRecord() { Id = 2, Name = "Beta", Status = "Archived" });
            ModelMgr modelMgr = CreateModelMgr(store);

            DataView view = modelMgr.CreateViewAndLoadRecords<TestRecord>();

            Assert.AreEqual(2, view.Table.Rows.Count);
            Assert.AreEqual(2, modelMgr.GetRecords<TestRecord>().Count);
            Assert.AreEqual("Alpha", view.Table.Rows[0]["Name"]);
        }

        [TestMethod]
        public void ModelTable_AddingRow_PersistsAndSetsIdentity()
        {
            FakeEntityStore store = new FakeEntityStore();
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateView<TestRecord>();
            ModelTable<TestRecord> table = new ModelTable<TestRecord>(modelMgr, new FakeEntitySessionFactory(store), view.Table, modelMgr.GetEntityRecordCollection<TestRecord>());

            DataRow row = view.Table.NewRow();
            row["Name"] = "Gamma";
            row["Status"] = "Pending";
            view.Table.Rows.Add(row);

            Assert.AreEqual(1, store.InsertCalls);
            Assert.AreEqual(1, store.Query<TestRecord>().Count);
            Assert.AreEqual(1, row["Id"]);
            Assert.AreEqual("Gamma", store.Query<TestRecord>().Single().Name);

            table.Dispose();
        }

        [TestMethod]
        public void ModelTable_ChangingExistingRow_UpdatesSessionAndModel()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateViewAndLoadRecords<TestRecord>();
            ModelTable<TestRecord> table = new ModelTable<TestRecord>(modelMgr, new FakeEntitySessionFactory(store), view.Table, modelMgr.GetEntityRecordCollection<TestRecord>());

            view.Table.Rows[0]["Status"] = "Archived";

            Assert.AreEqual(1, store.UpdateCalls);
            Assert.AreEqual("Archived", modelMgr.GetRecords<TestRecord>().Single().Status);

            table.Dispose();
        }

        [TestMethod]
        public void ModelTable_DeletingExistingRow_DeletesSessionEntity()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateViewAndLoadRecords<TestRecord>();
            ModelTable<TestRecord> table = new ModelTable<TestRecord>(modelMgr, new FakeEntitySessionFactory(store), view.Table, modelMgr.GetEntityRecordCollection<TestRecord>());

            view.Table.Rows[0].Delete();

            Assert.AreEqual(1, store.DeleteCalls);
            Assert.AreEqual(0, store.Query<TestRecord>().Count);

            table.Dispose();
        }

        [TestMethod]
        public void ModelView_DoesNotPersistChanges()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateViewAndLoadRecords<TestRecord>();
            ModelView<TestRecord> table = new ModelView<TestRecord>(modelMgr, new FakeEntitySessionFactory(store), view.Table, modelMgr.GetEntityRecordCollection<TestRecord>());
            store.ResetCounters();

            view.Table.Rows[0]["Status"] = "Archived";
            view.Table.Rows[0].Delete();
            DataRow row = view.Table.NewRow();
            row["Name"] = "Beta";
            row["Status"] = "Pending";
            view.Table.Rows.Add(row);

            Assert.AreEqual(0, store.InsertCalls);
            Assert.AreEqual(0, store.UpdateCalls);
            Assert.AreEqual(0, store.DeleteCalls);

            table.Dispose();
        }

        [TestMethod]
        public void LoadRecords_WithEntityType_UsesTypeBasedQuery()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateView<TestRecord>();

            List<IEntity> records = modelMgr.LoadRecords(typeof(TestRecord), view);

            Assert.AreEqual(1, store.TypeQueryCalls);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(1, view.Table.Rows.Count);
        }

        [TestMethod]
        public void UpdateRow_UsesProgrammaticUpdateWithoutCallingSessionUpdate()
        {
            FakeEntityStore store = new FakeEntityStore();
            store.Seed(new TestRecord() { Id = 1, Name = "Alpha", Status = "Active" });
            ModelMgr modelMgr = CreateModelMgr(store);
            DataView view = modelMgr.CreateViewAndLoadRecords<TestRecord>();
            ModelTable<TestRecord> table = new ModelTable<TestRecord>(modelMgr, new FakeEntitySessionFactory(store), view.Table, modelMgr.GetEntityRecordCollection<TestRecord>());
            TestRecord record = modelMgr.GetRecords<TestRecord>().Single();
            store.ResetCounters();

            record.Name = "Gamma";
            modelMgr.UpdateRow(record);

            Assert.AreEqual(0, store.UpdateCalls);
            Assert.AreEqual("Gamma", view.Table.Rows[0]["Name"]);

            table.Dispose();
        }

        private static ModelMgr CreateModelMgr(FakeEntityStore store)
        {
            return new ModelMgr(new FakeEntitySessionFactory(store));
        }

        private sealed class FakeEntitySessionFactory : IEntitySessionFactory
        {
            private readonly FakeEntityStore store;

            public FakeEntitySessionFactory(FakeEntityStore store)
            {
                this.store = store;
            }

            public IEntitySession CreateSession()
            {
                return new FakeEntitySession(store);
            }
        }

        private sealed class FakeEntitySession : IEntitySession
        {
            private readonly FakeEntityStore store;

            public FakeEntitySession(FakeEntityStore store)
            {
                this.store = store;
            }

            public void Dispose()
            {
            }

            public List<T> Query<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                IEnumerable<T> records = store.Query<T>();
                return whereClause == null ? records.ToList() : records.AsQueryable().Where(whereClause).ToList();
            }

            public List<IEntity> Query(Type entityType)
            {
                store.TypeQueryCalls++;
                return store.Query(entityType);
            }

            public List<IEntity> Query(Type entityType, Func<IEntity, bool> whereClause)
            {
                List<IEntity> records = Query(entityType);
                return whereClause == null ? records : records.Where(whereClause).ToList();
            }

            public List<T> SqlQuery<T>(string sql, params object[] parameters) where T : class, IEntity
            {
                return Query<T>();
            }

            public T Single<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                return Query(whereClause).Single();
            }

            public T SingleOrDefault<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                return Query(whereClause).SingleOrDefault();
            }

            public T FirstOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                IEnumerable<T> records = Query(whereClause);
                return records.AsQueryable().OrderBy(orderBy).FirstOrDefault();
            }

            public T LastOrDefault<T, TOrderBy>(Expression<Func<T, TOrderBy>> orderBy, Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                IEnumerable<T> records = Query(whereClause);
                return records.AsQueryable().OrderByDescending(orderBy).FirstOrDefault();
            }

            public int Count<T>(Expression<Func<T, bool>> whereClause = null) where T : class, IEntity
            {
                return Query(whereClause).Count;
            }

            public bool Exists<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity
            {
                return Query(whereClause).Any();
            }

            public int Insert<T>(T entity) where T : class, IEntity
            {
                return store.Insert(entity);
            }

            public int Insert(IEntity entity)
            {
                return store.Insert(entity);
            }

            public void InsertRange<T>(IEnumerable<T> entities) where T : class, IEntity
            {
                foreach (T entity in entities)
                {
                    store.Insert(entity);
                }
            }

            public void Update<T>(T entity) where T : class, IEntity
            {
                store.Update(entity);
            }

            public void Update(IEntity entity)
            {
                store.Update(entity);
            }

            public void Delete<T>(T entity) where T : class, IEntity
            {
                store.Delete(entity);
            }

            public void Delete(IEntity entity)
            {
                store.Delete(entity);
            }

            public void Delete<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity
            {
                store.Delete(whereClause);
            }
        }

        private sealed class FakeEntityStore
        {
            private readonly Dictionary<Type, List<IEntity>> itemsByType = new Dictionary<Type, List<IEntity>>();

            public int InsertCalls { get; set; }
            public int UpdateCalls { get; set; }
            public int DeleteCalls { get; set; }
            public int TypeQueryCalls { get; set; }

            public void Seed(IEntity entity)
            {
                GetList(entity.GetType()).Add(entity);
            }

            public List<T> Query<T>() where T : class, IEntity
            {
                return GetList(typeof(T)).Cast<T>().ToList();
            }

            public List<IEntity> Query(Type entityType)
            {
                return GetList(entityType).ToList();
            }

            public int Insert(IEntity entity)
            {
                InsertCalls++;

                if (!entity.Id.HasValue)
                {
                    entity.Id = GetList(entity.GetType()).Select(item => item.Id ?? 0).DefaultIfEmpty(0).Max() + 1;
                }

                GetList(entity.GetType()).Add(entity);

                return entity.Id.Value;
            }

            public void Update(IEntity entity)
            {
                UpdateCalls++;
            }

            public void Delete(IEntity entity)
            {
                DeleteCalls++;
                GetList(entity.GetType()).RemoveAll(item => item.Id == entity.Id);
            }

            public void Delete<T>(Expression<Func<T, bool>> whereClause) where T : class, IEntity
            {
                DeleteCalls++;
                List<T> matches = Query<T>().AsQueryable().Where(whereClause).ToList();
                GetList(typeof(T)).RemoveAll(item => matches.Any(match => match.Id == item.Id));
            }

            public void ResetCounters()
            {
                InsertCalls = 0;
                UpdateCalls = 0;
                DeleteCalls = 0;
                TypeQueryCalls = 0;
            }

            private List<IEntity> GetList(Type entityType)
            {
                if (!itemsByType.TryGetValue(entityType, out List<IEntity> items))
                {
                    items = new List<IEntity>();
                    itemsByType[entityType] = items;
                }

                return items;
            }
        }

        private sealed class TestRecord : MappedRecord, IEntity
        {
            [Key]
            [Column("Id")]
            public int? Id { get; set; }

            [Column("Name")]
            [DisplayField]
            [DisplayName("Name")]
            public string Name { get; set; }

            [Column("Status")]
            public string Status { get; set; }
        }
    }
}
