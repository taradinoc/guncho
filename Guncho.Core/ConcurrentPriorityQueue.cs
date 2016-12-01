using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guncho
{
    sealed class ConcurrentPriorityQueue<T> : PriorityQueue<T>
    {
        private readonly AsyncReaderWriterLock myLock = new AsyncReaderWriterLock();

        public ConcurrentPriorityQueue()
            : base()
        {
        }

        public ConcurrentPriorityQueue(int capacity)
            : base(capacity)
        {
        }

        public ConcurrentPriorityQueue(PriorityQueue<T> other)
            : base(other)
        {
        }

        public override int Capacity
        {
            get
            {
                return base.Capacity;
            }

            set
            {
                using (myLock.WriterLock())
                    base.Capacity = value;
            }
        }

        public async Task SetCapacityAsync(int value)
        {
            using (await myLock.WriterLockAsync())
                base.Capacity = value;
        }

        public override T Dequeue(out long priority)
        {
            using (myLock.WriterLock())
                return base.Dequeue(out priority);
        }

        public bool TryDequeue(out T item)
        {
            long dummy;
            return TryDequeue(out item, out dummy);
        }

        public bool TryDequeue(out T item, out long priority)
        {
            using (var key = myLock.UpgradeableReaderLock())
            {
                if (Count == 0)
                {
                    item = default(T);
                    priority = default(long);
                    return false;
                }

                using (key.Upgrade())
                {
                    item = base.Dequeue(out priority);
                    return true;
                }
            }
        }

        public class AsyncDequeueResult
        {
            public readonly bool Success;
            public readonly T Item;
            public readonly long Priority;

            internal static readonly AsyncDequeueResult Failed = new AsyncDequeueResult();

            private AsyncDequeueResult()
            {
                this.Success = false;
            }

            internal AsyncDequeueResult(T item, long priority)
            {
                this.Success = true;
                this.Item = item;
                this.Priority = priority;
            }
        }

        public async Task<AsyncDequeueResult> TryDequeueAsync()
        {
            using (var key = await myLock.UpgradeableReaderLockAsync())
            {
                if (Count == 0)
                    return AsyncDequeueResult.Failed;

                using (await key.UpgradeAsync())
                {
                    long priority;
                    var item = base.Dequeue(out priority);
                    return new AsyncDequeueResult(item, priority);
                }
            }
        }

        public override void Enqueue(T item, long priority)
        {
            using (myLock.WriterLock())
                base.Enqueue(item, priority);
        }

        public async Task EnqueueAsync(T item, long priority)
        {
            using (await myLock.WriterLockAsync())
                base.Enqueue(item, priority);
        }

        protected override void InternalExport(out Entry[] heap, out int count)
        {
            using (myLock.ReaderLock())
            {
                count = this.Count;
                heap = (Entry[])this.heap.Clone();
            }
        }

        public override T Peek(out long priority)
        {
            using (myLock.ReaderLock())
                return base.Peek(out priority);
        }

        public bool TryPeek(out T item)
        {
            long dummy;
            return TryPeek(out item, out dummy);
        }

        public bool TryPeek(out T item, out long priority)
        {
            using (var key = myLock.UpgradeableReaderLock())
            {
                if (Count == 0)
                {
                    item = default(T);
                    priority = default(long);
                    return false;
                }

                using (key.Upgrade())
                {
                    item = base.Peek(out priority);
                    return true;
                }
            }
        }

        public async Task<AsyncDequeueResult> TryPeekAsync()
        {
            using (var key = await myLock.UpgradeableReaderLockAsync())
            {
                if (Count == 0)
                    return AsyncDequeueResult.Failed;

                using (await key.UpgradeAsync())
                {
                    long priority;
                    var item = base.Peek(out priority);
                    return new AsyncDequeueResult(item, priority);
                }
            }
        }

        public async Task<IEnumerator<T>> GetEnumeratorAsync()
        {
            using (await myLock.ReaderLockAsync())
            {
                var heapCopy = (Entry[])heap.Clone();
                return CloneAsPriorityQueue().GetEnumerator();
            }
        }
    }
}
