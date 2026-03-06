using System;

namespace Clifton.Core.Data.Abstractions
{
    public interface ICreateUpdate
    {
        DateTime? CreatedOn { get; set; }
        DateTime? UpdatedOn { get; set; }
    }
}
