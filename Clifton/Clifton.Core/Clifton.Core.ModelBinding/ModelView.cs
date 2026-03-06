using System.Collections.Generic;
using System.Data;

using Clifton.Core.Data.Abstractions;

namespace Clifton.Core.ModelBinding
{
    public class ModelView<T> : ModelTable<T> where T : MappedRecord, IEntity, new()
    {
        public ModelView(ModelMgr modelMgr, IEntitySessionFactory sessionFactory, DataTable backingTable, List<IEntity> modelCollection)
            : base(modelMgr, sessionFactory, backingTable, modelCollection)
        {
        }

        protected override void Insert(T createdInstance)
        {
        }

        protected override void Delete(IEntity item)
        {
        }

        protected override void Update(IEntity instance)
        {
        }
    }
}
