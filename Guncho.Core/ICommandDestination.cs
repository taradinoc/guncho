namespace Guncho
{
    public interface ICommandDestination<in TInput, out TOutput>
    {
        void QueueInput(TInput line);
        TOutput SendAndGet(TInput line);
    }
}