namespace Five.Architecture
{
    public interface IResultProvider<T>
    {
        T GetResult();
    }
}