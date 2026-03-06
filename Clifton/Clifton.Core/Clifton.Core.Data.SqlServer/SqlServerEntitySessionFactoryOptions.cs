using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

namespace Clifton.Core.Data.SqlServer
{
    public sealed class SqlServerEntitySessionFactoryOptions
    {
        public string ConnectionString { get; set; }
        public IEnumerable<Type> EntityTypes { get; set; }
        public Action<ModelBuilder> ConfigureModel { get; set; }
    }
}
