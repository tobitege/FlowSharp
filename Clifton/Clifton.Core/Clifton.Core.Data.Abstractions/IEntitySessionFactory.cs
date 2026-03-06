namespace Clifton.Core.Data.Abstractions
{
    public interface IEntitySessionFactory
    {
        IEntitySession CreateSession();
    }
}
