using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Guncho
{
    /// <summary>
    /// Implements a priority queue based on a min-heap, which always dequeues
    /// the item with the lowest priority value.
    /// </summary>
    /// <typeparam name="T">The element type of the queue.</typeparam>
    class PriorityQueue<T> : IEnumerable<T>, ICloneable
    {
        protected struct Entry
        {
            public T Item;
            public long Priority;

            public Entry(T item, long priority)
            {
                this.Item = item;
                this.Priority = priority;
            }
        }

        private int count;
        protected Entry[] heap;

        /// <summary>
        /// Creates a new instance with the default initial capacity.
        /// </summary>
        public PriorityQueue()
            : this(10)
        {
        }

        /// <summary>
        /// Creates a new instance with the given capacity.
        /// </summary>
        /// <param name="capacity">The number of items the queue will
        /// initially be able to contain.</param>
        public PriorityQueue(int capacity)
        {
            Contract.Requires(capacity >= 0);

            heap = new Entry[capacity];
        }

        /// <summary>
        /// Creates a new instance containing the same items as another queue.
        /// </summary>
        /// <param name="other">The other queue.</param>
        public PriorityQueue(PriorityQueue<T> other)
        {
            other.InternalExport(out heap, out count);
        }

        protected PriorityQueue(Entry[] heap, int count)
        {
            Contract.Requires(heap != null);
            Contract.Requires(count >= 0);
            Contract.Requires(count <= heap.Length);

            this.count = count;
            this.heap = heap;
        }

        protected PriorityQueue<T> CloneAsPriorityQueue()
        {
            int count;
            Entry[] heap;

            InternalExport(out heap, out count);

            return new PriorityQueue<T>(heap, count);
        }

        protected virtual void InternalExport(out Entry[] heap, out int count)
        {
            count = this.count;
            heap = (Entry[])this.heap.Clone();
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(heap != null);
            Contract.Invariant(Count >= 0);
            Contract.Invariant(Count <= Capacity);
        }

        /// <summary>
        /// Adds an item to the queue at the given priority.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <param name="priority">The item's priority. A lower value means
        /// the item will be dequeued sooner.</param>
        public virtual void Enqueue(T item, long priority)
        {
            count++;

            if (count > Capacity)
                Capacity = Capacity * 2 + 1;

            BubbleUp(count - 1, new Entry(item, priority));
        }

        /// <summary>
        /// Removes the lowest-priority item from the queue and returns it,
        /// along with its priority value.
        /// </summary>
        /// <returns>The item that was dequeued.</returns>
        public T Dequeue()
        {
            long dummy;
            return Dequeue(out dummy);
        }

        /// <summary>
        /// Removes the lowest-priority item from the queue and returns it,
        /// along with its priority value.
        /// </summary>
        /// <param name="priority">The priority value of the item. By
        /// definition, no other item in the queue has a lower priority than this
        /// one.</param>
        /// <returns>The item that was dequeued.</returns>
        public virtual T Dequeue(out long priority)
        {
            if (count == 0)
                throw new InvalidOperationException("Queue is empty");

            T result = heap[0].Item;
            priority = heap[0].Priority;
            count--;
            TrickleDown(0, heap[count]);
            heap[count].Item = default(T);
            return result;
        }

        /// <summary>
        /// Gets the lowest-priority item in the queue, without removing it.
        /// </summary>
        /// <returns>The lowest-priority item.</returns>
        public T Peek()
        {
            long dummy;
            return Peek(out dummy);
        }

        /// <summary>
        /// Gets the lowest-priority item in the queue, without removing it,
        /// along with its priority value.
        /// </summary>
        /// <param name="priority">The priority value of the item. By
        /// definition, no other item in the queue has a lower priority than this
        /// one.</param>
        /// <returns>The lowest-priority item.</returns>
        public virtual T Peek(out long priority)
        {
            if (count == 0)
                throw new InvalidOperationException("Queue is empty");

            priority = heap[0].Priority;
            return heap[0].Item;
        }

        /// <summary>
        /// Gets the number of items in the queue.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// Gets or sets the number of items the queue can currently hold
        /// without growing.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value being assigned is less than <see cref="Count"/>.
        /// </exception>
        public virtual int Capacity
        {
            get
            {
                return heap.Length;
            }
            set
            {
                if (value < count)
                    throw new ArgumentOutOfRangeException("value",
                        "Capacity may not be less than Count");

                if (value != heap.Length)
                {
                    Entry[] newHeap = new Entry[value];
                    Array.Copy(heap, newHeap, Math.Min(value, heap.Length));
                    heap = newHeap;
                }
            }
        }

        private void BubbleUp(int index, Entry entry)
        {
            int parent = (index - 1) / 2;

            while (index > 0 && heap[parent].Priority > entry.Priority)
            {
                heap[index] = heap[parent];
                index = parent;
                parent = (index - 1) / 2;
            }

            heap[index] = entry;
        }

        private void TrickleDown(int index, Entry entry)
        {
            int child = index * 2 + 1;

            while (child < count)
            {
                if (child + 1 < count && heap[child].Priority > heap[child + 1].Priority)
                    child++;

                heap[index] = heap[child];
                index = child;
                child = index * 2 + 1;
            }

            BubbleUp(index, entry);
        }

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            // enumerate in order
            PriorityQueue<T> copy = new PriorityQueue<T>(this);
            while (copy.Count > 0)
                yield return copy.Dequeue();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region ICloneable Members

        public object Clone()
        {
            return new PriorityQueue<T>(this);
        }

        #endregion

        public void CopyTo(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (index < 0)
                throw new ArgumentOutOfRangeException("index");

            if (array.Rank > 1)
                throw new ArgumentException("Array is multidimensional");
            else if (index >= array.Length)
                throw new ArgumentException("Index is past end of array");
            else if (count > array.Length - index)
                throw new ArgumentException("Array is too small");

            // copy in order
            foreach (T item in this)
                array[index++] = item;
        }
    }
}
