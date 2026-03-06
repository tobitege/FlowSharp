namespace Clifton.Core.Data.Abstractions
{
    public interface INamedEntity : IEntity
    {
        string Name { get; set; }
    }
}
