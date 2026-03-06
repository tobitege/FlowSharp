using System;

namespace Clifton.Core.ModelBinding
{
    public class DisplayFieldAttribute : Attribute { }
    public class UniqueAttribute : Attribute { }
    public class ReadOnlyAttribute : Attribute { }

    public class FormatAttribute : Attribute
    {
        public string Format { get; }

        public FormatAttribute(string format)
        {
            Format = format;
        }
    }

    public class DisplayNameAttribute : Attribute
    {
        public string DisplayName { get; }

        public DisplayNameAttribute(string name)
        {
            DisplayName = name;
        }
    }

    public class ActualTypeAttribute : Attribute
    {
        public string ActualTypeName { get; }

        public ActualTypeAttribute(string name)
        {
            ActualTypeName = name;
        }
    }

    public class MappedColumnAttribute : Attribute
    {
        public string Name { get; }

        public MappedColumnAttribute(string name)
        {
            Name = name;
        }
    }

    public class LookupAttribute : Attribute
    {
        public Type ModelType { get; set; }
        public string DisplayField { get; set; }
        public string ValueField { get; set; }

        public LookupAttribute()
        {
            DisplayField = "Name";
            ValueField = "Id";
        }
    }

    public class ForeignKeyAttribute : Attribute
    {
        public string ForeignKeyTable { get; }
        public string ForeignKeyColumn { get; }

        public ForeignKeyAttribute(string fkTable, string fkColumn)
        {
            ForeignKeyTable = fkTable;
            ForeignKeyColumn = fkColumn;
        }
    }
}
