using Nito.AsyncEx;

namespace Guncho
{
    /// <summary>
    /// Extension methods for working with multiple AsyncProducerConsumerQueues.
    /// Provides functionality missing from Nito.AsyncEx v5.
    /// </summary>
    public static class AsyncQueueExtensions
    {
        /// <summary>
        /// Waits for any queue to have an item available, then dequeues from that queue.
        /// This implementation uses a semaphore-based approach: we peek at queues in a loop,
        /// and when we find one with items, we dequeue from it atomically.
        /// </summary>
        public static async Task<DequeueResult<T>> TryDequeueFromAnyAsync<T>(
            this AsyncProducerConsumerQueue<T>[] queues,
            CancellationToken cancellationToken = default)
        {
            if (queues == null || queues.Length == 0)
                throw new ArgumentException("Must provide at least one queue", nameof(queues));

            // Keep trying round-robin through the queues until we get an item
            while (!cancellationToken.IsCancellationRequested)
            {
                for (int i = 0; i < queues.Length; i++)
                {
                    // DequeueAsync in v5 throws on empty queue, so we need to handle that
                    try
                    {
                        // Try with immediate cancellation to see if item is available
                        using var cts = new CancellationTokenSource();
                        cts.Cancel();
                        var item = await queues[i].DequeueAsync(cts.Token);
                        return new DequeueResult<T>(true, item, queues[i], i);
                    }
                    catch (OperationCanceledException)
                    {
                        // Queue was empty, try next one
                    }
                }
                
                // No items in any queue; wait briefly before polling again
                // This avoids a tight spin loop while still being reasonably responsive
                await Task.Delay(1, cancellationToken);
            }
            
            cancellationToken.ThrowIfCancellationRequested();
            return new DequeueResult<T>(false, default!, null!, -1);
        }
    }

    /// <summary>
    /// Result of attempting to dequeue from multiple queues.
    /// </summary>
    public struct DequeueResult<T>
    {
        public bool Success { get; }
        public T Item { get; }
        public AsyncProducerConsumerQueue<T> Queue { get; }
        public int QueueIndex { get; }

        public DequeueResult(bool success, T item, AsyncProducerConsumerQueue<T> queue, int queueIndex)
        {
            Success = success;
            Item = item;
            Queue = queue;
            QueueIndex = queueIndex;
        }
    }
}
