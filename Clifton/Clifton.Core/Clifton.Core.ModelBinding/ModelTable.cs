using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

using Clifton.Core.Data.Abstractions;
using Clifton.Core.ExtensionMethods;

namespace Clifton.Core.ModelBinding
{
    public class RowDeletedEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
        public bool Handled { get; set; }
    }

    public class RowAddingEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
    }

    public class RowChangedEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
        public string ColumnName { get; set; }
        public bool Handled { get; set; }
    }

    public class RowChangeFinalizedEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
        public string ColumnName { get; set; }
    }

    public class RowAddFinalizedEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
    }

    public class RowDeleteFinalizedEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
    }

    public class ColumnChangingEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
        public string ColumnName { get; set; }
        public object ProposedValue { get; set; }
    }

    public class RowChangingEventArgs : EventArgs
    {
        public IEntity Entity { get; set; }
        public string ColumnName { get; set; }
        public bool Handled { get; set; }
        public bool ReplaceInstance { get; set; }
        public IEntity NewInstance { get; set; }
    }

    public interface IModelTable
    {
        void BeginProgrammaticUpdate();
        void EndProgrammaticUpdate();
        void Replace(IEntity oldEntity, IEntity withEntity);
    }

    public class ModelTable<T> : IModelTable, IDisposable where T : MappedRecord, IEntity, new()
    {
        public DataTable Table => dataTable;

        public const string PK_FIELD = "Id";

        public event EventHandler<ColumnChangingEventArgs> ColumnChanging;
        public event EventHandler<RowDeletedEventArgs> RowDeleted;
        public event EventHandler<RowAddingEventArgs> RowAdding;
        public event EventHandler<RowChangedEventArgs> RowChanged;
        public event EventHandler<RowChangingEventArgs> RowChanging;
        public event EventHandler<RowChangeFinalizedEventArgs> RowChangeFinalized;
        public event EventHandler<RowAddFinalizedEventArgs> RowAddFinalized;
        public event EventHandler<RowDeleteFinalizedEventArgs> RowDeleteFinalized;

        protected DataTable dataTable;
        protected T newInstance;
        protected List<IEntity> items;
        protected ModelMgr modelMgr;
        protected IEntitySessionFactory sessionFactory;
        protected bool programmaticUpdate;

        public ModelTable(ModelMgr modelMgr, IEntitySessionFactory sessionFactory, DataTable backingTable, List<IEntity> modelCollection)
        {
            this.modelMgr = modelMgr;
            this.sessionFactory = sessionFactory;
            dataTable = backingTable;
            items = modelCollection;
            WireUpEvents(dataTable);
            RegisterWithModelManager();
        }

        public void Dispose()
        {
            dataTable.ColumnChanging -= Table_ColumnChanging;
            dataTable.ColumnChanged -= Table_ColumnChanged;
            dataTable.RowDeleted -= Table_RowDeleted;
            dataTable.TableNewRow -= Table_TableNewRow;
            dataTable.RowChanged -= Table_RowChanged;
            UnregisterWithModelManager();
        }

        public void Replace(IEntity oldEntity, IEntity newEntity)
        {
            int index = items.IndexOf(record =>
            {
                if (record == null || oldEntity == null)
                {
                    return false;
                }

                return ((MappedRecord)record).Row == ((MappedRecord)oldEntity).Row;
            });

            if (index != -1)
            {
                items[index] = newEntity;
            }
        }

        public void ResetItems(List<IEntity> modelCollection)
        {
            items = modelCollection;
        }

        public void BeginProgrammaticUpdate()
        {
            programmaticUpdate = true;
        }

        public void EndProgrammaticUpdate()
        {
            programmaticUpdate = false;
        }

        protected void WireUpEvents(DataTable table)
        {
            table.ColumnChanging += Table_ColumnChanging;
            table.ColumnChanged += Table_ColumnChanged;
            table.RowDeleted += Table_RowDeleted;
            table.TableNewRow += Table_TableNewRow;
            table.RowChanged += Table_RowChanged;
        }

        protected void RegisterWithModelManager()
        {
            modelMgr.Register<T>(this);
        }

        protected void UnregisterWithModelManager()
        {
            modelMgr.Unregister<T>(this);
        }

        protected void Table_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            if (programmaticUpdate)
            {
                return;
            }

            switch (e.Action)
            {
                case DataRowAction.Add:
                    RowAdding.Fire(this, new RowAddingEventArgs() { Entity = newInstance });
                    items.Add(newInstance);
                    Insert(newInstance);
                    programmaticUpdate = true;
                    e.Row[PK_FIELD] = newInstance.Id;
                    programmaticUpdate = false;
                    RowAddFinalized.Fire(this, new RowAddFinalizedEventArgs() { Entity = newInstance });
                    break;
            }
        }

        protected void Table_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            if (!programmaticUpdate)
            {
                newInstance = new T()
                {
                    Row = e.Row
                };
            }
        }

        protected void Table_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            if (programmaticUpdate)
            {
                return;
            }

            IEntity item = items.SingleOrDefault(record => ((MappedRecord)record).Row == e.Row);

            if (item == null)
            {
                return;
            }

            RowDeletedEventArgs args = new RowDeletedEventArgs() { Entity = item };
            RowDeleted.Fire(this, args);

            if (args.Handled)
            {
                return;
            }

            items.Remove(item);
            Delete(item);
            RowDeleteFinalized.Fire(this, new RowDeleteFinalizedEventArgs() { Entity = item });
        }

        protected virtual void Insert(T createdInstance)
        {
            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Insert(createdInstance);
            }
        }

        protected virtual void Delete(IEntity item)
        {
            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Delete(item);
            }
        }

        protected virtual void Update(IEntity instance)
        {
            using (IEntitySession session = sessionFactory.CreateSession())
            {
                session.Update(instance);
            }
        }

        protected virtual void Table_ColumnChanging(object sender, DataColumnChangeEventArgs e)
        {
            if (programmaticUpdate)
            {
                return;
            }

            IEntity instance = e.Row.RowState == DataRowState.Detached ? newInstance : items.SingleOrDefault(record => ((MappedRecord)record).Row == e.Row);
            ColumnChanging.Fire(this, new ColumnChangingEventArgs()
            {
                Entity = instance,
                ColumnName = e.Column.ColumnName,
                ProposedValue = e.ProposedValue
            });
        }

        protected virtual void Table_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (programmaticUpdate)
            {
                return;
            }

            IEntity instance = newInstance;

            if (e.Row.RowState != DataRowState.Detached)
            {
                instance = items.SingleOrDefault(record => ((MappedRecord)record).Row == e.Row);
            }

            if (e.Row.RowState != DataRowState.Detached)
            {
                if (instance != null)
                {
                    var property = instance.GetType().GetProperty(e.Column.ColumnName);

                    if (property != null)
                    {
                        object oldValue = property.GetValue(instance);

                        if (((oldValue != null) || (e.ProposedValue == DBNull.Value)) &&
                            ((oldValue == null) || (oldValue.Equals(e.ProposedValue))))
                        {
                            return;
                        }
                    }
                }

                RowChangingEventArgs rowChangingArgs = new RowChangingEventArgs() { Entity = instance, ColumnName = e.Column.ColumnName };
                RowChanging.Fire(this, rowChangingArgs);

                if (!rowChangingArgs.Handled)
                {
                    if (rowChangingArgs.ReplaceInstance)
                    {
                        instance = rowChangingArgs.NewInstance;
                    }

                    modelMgr.UpdateRecordField(instance, e.Column.ColumnName, e.ProposedValue);
                }

                ExtDataColumn column = (ExtDataColumn)e.Column;

                if (!column.IsDbColumn && column.MappedColumn == null)
                {
                    return;
                }

                RowChangedEventArgs rowChangedArgs = new RowChangedEventArgs() { Entity = instance, ColumnName = e.Column.ColumnName };
                RowChanged.Fire(this, rowChangedArgs);

                if (rowChangedArgs.Handled)
                {
                    return;
                }

                Update(instance);
                RowChangeFinalized.Fire(this, new RowChangeFinalizedEventArgs() { Entity = instance, ColumnName = e.Column.ColumnName });
                return;
            }

            if (instance == null)
            {
                return;
            }

            var targetProperty = instance.GetType().GetProperty(e.Column.ColumnName);

            if (targetProperty == null)
            {
                return;
            }

            object existingValue = targetProperty.GetValue(instance);

            if (((existingValue == null) && (e.ProposedValue != DBNull.Value)) ||
                ((existingValue != null) && (!existingValue.Equals(e.ProposedValue))))
            {
                modelMgr.UpdateRecordField(instance, e.Column.ColumnName, e.ProposedValue);
            }
        }
    }
}
