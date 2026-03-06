using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Clifton.Core.Assertions;
using Clifton.Core.Data.Abstractions;
using Clifton.Core.ExtensionMethods;

namespace Clifton.Core.ModelBinding
{
    public class ExtDataColumn : DataColumn
    {
        public bool Visible { get; set; }
        public bool IsDbColumn { get; set; }
        public LookupAttribute Lookup { get; set; }
        public string MappedColumn { get; set; }
        public string Format { get; set; }
        public string ActualType { get; set; }
        public int FieldMaxLength { get; set; }

        public string ActualColumnName => MappedColumn ?? ColumnName;

        public ExtDataColumn(string colName, Type colType, bool visible, bool isDbColumn, string format, string actualType, int maxLength, LookupAttribute lookup = null)
            : base(colName, colType)
        {
            Visible = visible;
            IsDbColumn = isDbColumn;
            Lookup = lookup;
            Format = format;
            ActualType = actualType;
            FieldMaxLength = maxLength;
        }
    }

    public class Field
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string MappedColumn { get; set; }
        public Type Type { get; set; }
        public string ActualType { get; set; }
        public bool ReadOnly { get; set; }
        public bool Visible { get; set; }
        public bool IsColumn { get; set; }
        public bool IsDisplayField { get; set; }
        public string Format { get; set; }
        public LookupAttribute Lookup { get; set; }
        public int MaxLength { get; set; }

        public bool IsTableField => IsColumn || IsDisplayField;
    }

    public class ModelPropertyChangedEventArgs : EventArgs
    {
        public string FieldName { get; set; }
        public object Value { get; set; }
        public object OldValue { get; set; }
    }

    public class ModelManagerDataSet
    {
        public DataSet DataSet => dataset;

        protected ModelMgr modelMgr;
        protected DataSet dataset;
        protected List<Type> entityTypes;
        protected Dictionary<Type, DataTable> typeTableMap;

        public ModelManagerDataSet(ModelMgr modelMgr)
        {
            this.modelMgr = modelMgr;
            dataset = new DataSet();
            entityTypes = new List<Type>();
            typeTableMap = new Dictionary<Type, DataTable>();
        }

        public ModelManagerDataSet WithTable<T>(Expression<Func<T, bool>> whereClause = null) where T : MappedRecord, IEntity
        {
            DataTable table = modelMgr.CreateViewAndLoadRecords(whereClause).Table;
            dataset.Tables.Add(table);
            Type entityType = typeof(T);
            entityTypes.Add(entityType);
            typeTableMap[entityType] = table;

            return this;
        }

        public ModelManagerDataSet BuildAssociations()
        {
            for (int i = 0; i < dataset.Tables.Count; i++)
            {
                DataTable table = dataset.Tables[i];
                Type entityType = typeTableMap.Single(kvp => kvp.Value == table).Key;

                var foreignKeys = entityType.GetProperties().
                    Where(prop => Attribute.IsDefined(prop, typeof(ForeignKeyAttribute))).
                    Select(prop => new
                    {
                        Property = prop,
                        Attribute = (ForeignKeyAttribute)prop.GetCustomAttribute(typeof(ForeignKeyAttribute)),
                    });

                foreach (var foreignKey in foreignKeys)
                {
                    if (!typeTableMap.Keys.TryGetSingle(type => type.Name == foreignKey.Attribute.ForeignKeyTable, out Type fkType))
                    {
                        continue;
                    }

                    int index = typeTableMap.IndexOf(kvp => kvp.Key == fkType);

                    if (index < i)
                    {
                        DataTable parent = typeTableMap[fkType];
                        dataset.Relations.Add(parent.TableName + "-" + table.TableName, parent.Columns[foreignKey.Attribute.ForeignKeyColumn], table.Columns[foreignKey.Property.Name]);
                    }
                }
            }

            return this;
        }
    }

    public static class ModelMgrExtensionMethods
    {
        public static ModelManagerDataSet CreateDataSet(this ModelMgr modelMgr)
        {
            return new ModelManagerDataSet(new ModelMgr(modelMgr.SessionFactory));
        }
    }

    public class ModelMgr
    {
        public event EventHandler<ModelPropertyChangedEventArgs> PropertyChanged;

        public IEntitySessionFactory SessionFactory => sessionFactory;

        protected Dictionary<Type, List<IEntity>> mappedRecords;
        protected Dictionary<Type, List<IModelTable>> modelTables;
        protected Dictionary<Type, DataView> modelViewMap;
        protected IEntitySessionFactory sessionFactory;

        public ModelMgr(IEntitySessionFactory sessionFactory)
        {
            this.sessionFactory = sessionFactory;
            mappedRecords = new Dictionary<Type, List<IEntity>>();
            modelTables = new Dictionary<Type, List<IModelTable>>();
            modelViewMap = new Dictionary<Type, DataView>();
        }

        public void DisposeOfAllTables()
        {
            modelTables.ForEach(kvp => kvp.Value.Cast<IDisposable>().ForEach(modelTable => modelTable.Dispose()));
        }

        public void RemoveView<T>()
        {
            Type entityType = typeof(T);
            mappedRecords.Remove(entityType);
            modelTables.Remove(entityType);
            modelViewMap.Remove(entityType);
        }

        public void Replace(MappedRecord oldEntity, MappedRecord withEntity)
        {
            Type entityType = oldEntity.GetType();
            List<IEntity> entities = mappedRecords[entityType];
            int index = entities.Cast<MappedRecord>().IndexOf(entity => entity.Row == oldEntity.Row);

            if (index != -1)
            {
                entities[index] = (IEntity)withEntity;
            }

            modelTables[entityType].ForEach(modelTable => modelTable.Replace((IEntity)oldEntity, (IEntity)withEntity));
        }

        public void Register<T>() where T : MappedRecord, IEntity
        {
            Register(typeof(T));
        }

        public void Clear<T>() where T : MappedRecord, IEntity
        {
            Clear(typeof(T));
        }

        public void Clear(Type entityType)
        {
            mappedRecords[entityType] = new List<IEntity>();
        }

        public DataView CreateViewAndLoadRecords<T>(Expression<Func<T, bool>> whereClause = null) where T : MappedRecord, IEntity
        {
            DataView view = CreateView<T>();
            LoadRecords(view, whereClause);

            return view;
        }

        public List<IEntity> LoadRecords<T>(DataView dataView, Expression<Func<T, bool>> whereClause = null) where T : MappedRecord, IEntity
        {
            Clear<T>();
            Type entityType = typeof(T);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Query(whereClause).ForEach(record => AppendRow(dataView, record));
            }

            return mappedRecords[entityType];
        }

        public List<IEntity> LoadRecordsFromSql<T>(DataView dataView, string sql) where T : MappedRecord, IEntity
        {
            Clear<T>();
            Type entityType = typeof(T);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.SqlQuery<T>(sql).ForEach(record => AppendRow(dataView, record));
            }

            return mappedRecords[entityType];
        }

        public List<IEntity> LoadRecords(Type entityType, DataView dataView)
        {
            Clear(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                List<IEntity> records = session.Query(entityType);
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(dataView, entityType, record));

                return records.ToList();
            }
        }

        public List<IEntity> LoadRecords(Type entityType, DataView dataView, Func<MappedRecord, bool> where)
        {
            Clear(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                List<IEntity> records = session.Query(entityType, entity => where((MappedRecord)entity));
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(dataView, entityType, record));

                return records.ToList();
            }
        }

        public List<IEntity> LoadRecords(Type entityType, DataView dataView, out List<IEntity> records)
        {
            Clear(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                records = session.Query(entityType);
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(dataView, entityType, record));

                return records.ToList();
            }
        }

        public List<IEntity> LoadRecords(Type entityType, DataView dataView, Func<MappedRecord, bool> where, out List<IEntity> records)
        {
            Clear(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                records = session.Query(entityType, entity => where((MappedRecord)entity));
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(dataView, entityType, record));

                return records.ToList();
            }
        }

        public DataView LoadDecoupledView(Type entityType)
        {
            DataView view = CreateDecoupledView(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Query(entityType).Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(view, entityType, record));
            }

            return view;
        }

        public DataView LoadDecoupledView(Type entityType, Func<MappedRecord, bool> where)
        {
            DataView view = CreateDecoupledView(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Query(entityType, entity => where((MappedRecord)entity)).Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(view, entityType, record));
            }

            return view;
        }

        public DataView LoadDecoupledView(Type entityType, out List<IEntity> records)
        {
            DataView view = CreateDecoupledView(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                records = session.Query(entityType);
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(view, entityType, record));
            }

            return view;
        }

        public DataView LoadDecoupledView(Type entityType, Func<MappedRecord, bool> where, out List<IEntity> records)
        {
            DataView view = CreateDecoupledView(entityType);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                records = session.Query(entityType, entity => where((MappedRecord)entity));
                records.Cast<MappedRecord>().ForEach(record => AppendDecoupledRow(view, entityType, record));
            }

            return view;
        }

        public List<IEntity> ReloadRecords<T>(DataView dataView, Expression<Func<T, bool>> whereClause = null) where T : MappedRecord, IEntity
        {
            ClearView<T>();
            Type entityType = typeof(T);

            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Query(whereClause).ForEach(record => AppendRow(dataView, record));
            }

            return mappedRecords[entityType];
        }

        public void AddRecords<T>(DataView dataView) where T : MappedRecord, IEntity
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            mappedRecords[typeof(T)].ForEach(entity =>
            {
                DataRow row = NewRow(dataView, typeof(T), (MappedRecord)entity);
                dataView.Table.Rows.Add(row);
            });
        }

        public void DeleteAllRecords<T>(DataView dataView) where T : MappedRecord, IEntity
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            List<DataRow> rows = new List<DataRow>();

            foreach (DataRow row in dataView.Table.Rows)
            {
                rows.Add(row);
            }

            rows.ForEach(row => dataView.Table.Rows.Remove(row));
            Clear<T>();
        }

        public List<IEntity> GetEntityRecordCollection<T>() where T : MappedRecord, IEntity, new()
        {
            return mappedRecords[typeof(T)];
        }

        public List<IEntity> GetEntityRecordCollection(Type entityType)
        {
            return mappedRecords[entityType];
        }

        public List<T> GetRecords<T>() where T : MappedRecord, IEntity, new()
        {
            return mappedRecords[typeof(T)].Cast<T>().ToList();
        }

        public bool TryGetView<T>(out DataView dataView)
        {
            return modelViewMap.TryGetValue(typeof(T), out dataView);
        }

        public bool TryGetView(Type entityType, out DataView dataView)
        {
            return modelViewMap.TryGetValue(entityType, out dataView);
        }

        public DataView CreateView<T>() where T : MappedRecord, IEntity
        {
            return CreateView(typeof(T));
        }

        public DataView CreateView(Type entityType)
        {
            Register(entityType);
            DataTable table = new DataTable
            {
                TableName = entityType.Name
            };
            CreateColumns(table, GetFields(entityType));
            DataView dataView = new DataView(table);
            modelViewMap[entityType] = dataView;

            return dataView;
        }

        public void ClearView<T>() where T : MappedRecord, IEntity
        {
            ClearView(typeof(T));
        }

        public void ClearView(Type entityType)
        {
            modelTables[entityType].ForEach(modelTable => modelTable.BeginProgrammaticUpdate());
            modelViewMap[entityType].Table.Rows.Clear();
            mappedRecords[entityType].Clear();
            modelTables[entityType].ForEach(modelTable => modelTable.EndProgrammaticUpdate());
        }

        public void AppendRow<T>(DataView view, T model) where T : MappedRecord
        {
            AppendRow(view, model.GetType(), model);
        }

        public void InsertRow<T>(DataView view, T model) where T : MappedRecord
        {
            Assert.That(modelTables.ContainsKey(model.GetType()), "Model Manager does not know about " + model.GetType().Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            modelTables[model.GetType()].ForEach(modelTable => modelTable.BeginProgrammaticUpdate());
            DataRow row = NewRow(view, model.GetType(), model);
            view.Table.Rows.InsertAt(row, 0);
            modelTables[model.GetType()].ForEach(modelTable => modelTable.EndProgrammaticUpdate());
            AddRecordToCollection(model);
        }

        public void DeleteRecord<T>(DataView dataView, T model) where T : MappedRecord, IEntity
        {
            Assert.That(modelTables.ContainsKey(model.GetType()), "Model Manager does not know about " + model.GetType().Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            modelTables[model.GetType()].ForEach(modelTable => modelTable.BeginProgrammaticUpdate());
            dataView.Table.Rows.Remove(model.Row);
            mappedRecords[typeof(T)].Remove(mappedRecords[typeof(T)].Single(record => record == model));
            modelTables[model.GetType()].ForEach(modelTable => modelTable.EndProgrammaticUpdate());
        }

        public void UpdateRow<T>(T model) where T : MappedRecord
        {
            Assert.That(modelTables.ContainsKey(model.GetType()), "Model Manager does not know about " + model.GetType().Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            modelTables[model.GetType()].ForEach(modelTable => modelTable.BeginProgrammaticUpdate());

            foreach (Field field in GetFields<T>().Where(candidate => candidate.IsTableField))
            {
                object value = model.GetType().GetProperty(field.Name).GetValue(model);
                UpdateTableRowField(model.Row, field.Name, value);
            }

            modelTables[model.GetType()].ForEach(modelTable => modelTable.EndProgrammaticUpdate());
        }

        public void UpdateRecordField(IEntity record, string columnName, object value)
        {
            PropertyInfo property = record.GetType().GetProperty(columnName);
            object oldValue = property.GetValue(record);

            if (((oldValue == null) && (value != DBNull.Value)) ||
                ((oldValue != null) && (!oldValue.Equals(value))))
            {
                property.SetValue(record, DbNullConverter(value));
                PropertyChanged.Fire(record, new ModelPropertyChangedEventArgs() { FieldName = columnName, Value = value, OldValue = oldValue });
            }
        }

        public void FirePropertyChangedEvent(IEntity record, string columnName, object value, object oldValue)
        {
            PropertyChanged.Fire(record, new ModelPropertyChangedEventArgs() { FieldName = columnName, Value = value, OldValue = oldValue });
        }

        public bool TryGetRow<T>(List<T> items, Func<T, bool> predicate, out T record) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            record = items.SingleOrDefault(predicate);

            return record != null;
        }

        public bool TryGetRow<T>(Func<T, bool> predicate, out T record) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            record = mappedRecords[typeof(T)].Cast<T>().SingleOrDefault(predicate);

            return record != null;
        }

        public T GetRow<T>(Func<T, bool> predicate) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");

            return mappedRecords[typeof(T)].Cast<T>().SingleOrDefault(predicate);
        }

        public List<T> GetRows<T>(Func<T, bool> predicate) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");

            return mappedRecords[typeof(T)].Cast<T>().Where(predicate).ToList();
        }

        public T GetRow<T>(DataRow row) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");

            return mappedRecords[typeof(T)].Cast<T>().Single(record => record.Row == row);
        }

        public bool TryGetRow<T>(DataRow row, out T record) where T : MappedRecord
        {
            Assert.That(mappedRecords.ContainsKey(typeof(T)), "Model Manager does not know about " + typeof(T).Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            record = mappedRecords[typeof(T)].Cast<T>().SingleOrDefault(item => item.Row == row);

            return record != null;
        }

        public void UpdateTableRowField(DataRow row, string fieldName, object value)
        {
            lock (this)
            {
                if (row == null)
                {
                    return;
                }

                bool lastState = row.Table.Columns[fieldName].ReadOnly;
                row.Table.Columns[fieldName].ReadOnly = false;
                row[fieldName] = value ?? DBNull.Value;
                row.Table.Columns[fieldName].ReadOnly = lastState;
                row.Table.AcceptChanges();
            }
        }

        public void Register<T>(IModelTable modelTable) where T : IEntity
        {
            modelTables[typeof(T)].Add(modelTable);
        }

        public void Unregister<T>(IModelTable modelTable) where T : IEntity
        {
            modelTables[typeof(T)].Remove(modelTable);
        }

        protected object DbNullConverter(object value)
        {
            return value == DBNull.Value ? null : value;
        }

        protected void AppendRow(DataView view, Type entityType, MappedRecord record)
        {
            modelTables[entityType].ForEach(modelTable => modelTable.BeginProgrammaticUpdate());
            DataRow row = NewRow(view, entityType, record);
            view.Table.Rows.Add(row);
            AddRecordToCollection(record);
            modelTables[entityType].ForEach(modelTable => modelTable.EndProgrammaticUpdate());
        }

        protected DataView CreateDecoupledView(Type entityType)
        {
            DataTable table = new DataTable
            {
                TableName = entityType.Name
            };
            CreateColumns(table, GetFields(entityType));

            return new DataView(table);
        }

        protected void AppendDecoupledRow(DataView view, Type entityType, MappedRecord record)
        {
            view.Table.Rows.Add(NewRow(view, entityType, record));
        }

        protected void AddRecordToCollection(MappedRecord record)
        {
            Assert.That(mappedRecords.ContainsKey(record.GetType()), "Model Manager does not know about " + record.GetType().Name + ".\r\nCreate an instance of ModuleMgr with this record collection.");
            mappedRecords[record.GetType()].Add((IEntity)record);
        }

        protected void Register(Type entityType)
        {
            mappedRecords[entityType] = new List<IEntity>();
            modelTables[entityType] = new List<IModelTable>();
        }

        protected DataRow NewRow(DataView view, Type modelType, MappedRecord record)
        {
            DataRow row = view.Table.NewRow();

            foreach (Field field in GetFields(modelType).Where(candidate => candidate.IsTableField))
            {
                object value = modelType.GetProperty(field.Name).GetValue(record);
                row[field.Name] = value ?? DBNull.Value;
            }

            record.Row = row;

            return row;
        }

        protected List<Field> GetFields<T>()
        {
            return GetFields(typeof(T));
        }

        protected List<Field> GetFields(Type modelType)
        {
            var fields = from property in modelType.GetProperties()
                         where Attribute.IsDefined(property, typeof(ColumnAttribute)) || Attribute.IsDefined(property, typeof(DisplayFieldAttribute))
                         select new Field()
                         {
                             Name = property.Name,
                             DisplayName = Attribute.IsDefined(property, typeof(DisplayNameAttribute)) ? ((DisplayNameAttribute)property.GetCustomAttribute(typeof(DisplayNameAttribute))).DisplayName : property.Name,
                             Type = property.PropertyType,
                             ActualType = Attribute.IsDefined(property, typeof(ActualTypeAttribute)) ? ((ActualTypeAttribute)property.GetCustomAttribute(typeof(ActualTypeAttribute))).ActualTypeName : null,
                             MaxLength = Attribute.IsDefined(property, typeof(MaxLengthAttribute)) ? ((MaxLengthAttribute)property.GetCustomAttribute(typeof(MaxLengthAttribute))).Length : 0,
                             ReadOnly = Attribute.IsDefined(property, typeof(ReadOnlyAttribute)),
                             Visible = Attribute.IsDefined(property, typeof(DisplayFieldAttribute)),
                             IsColumn = Attribute.IsDefined(property, typeof(ColumnAttribute)),
                             IsDisplayField = Attribute.IsDefined(property, typeof(DisplayFieldAttribute)),
                             Format = Attribute.IsDefined(property, typeof(FormatAttribute)) ? ((FormatAttribute)property.GetCustomAttribute(typeof(FormatAttribute))).Format : null,
                             Lookup = Attribute.IsDefined(property, typeof(LookupAttribute)) ? (LookupAttribute)property.GetCustomAttribute(typeof(LookupAttribute)) : null,
                             MappedColumn = Attribute.IsDefined(property, typeof(MappedColumnAttribute)) ? ((MappedColumnAttribute)property.GetCustomAttribute(typeof(MappedColumnAttribute))).Name : null,
                         };

            return fields.ToList();
        }

        protected void CreateColumns(DataTable table, List<Field> fields)
        {
            foreach (Field field in fields.Where(candidate => candidate.IsTableField))
            {
                Type columnType = Nullable.GetUnderlyingType(field.Type) ?? field.Type;
                ExtDataColumn column = new ExtDataColumn(field.Name, columnType, field.Visible, field.IsColumn, field.Format, field.ActualType, field.MaxLength, field.Lookup)
                {
                    ReadOnly = field.ReadOnly,
                    Caption = field.DisplayName,
                    MappedColumn = field.MappedColumn,
                };
                table.Columns.Add(column);
            }
        }
    }
}
