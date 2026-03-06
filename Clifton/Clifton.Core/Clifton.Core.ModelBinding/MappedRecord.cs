using System.Data;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Clifton.Core.ModelBinding
{
    public abstract class MappedRecord
    {
        [IgnoreDataMember, XmlIgnore]
        public DataRow Row { get; set; }
    }
}
