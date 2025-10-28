using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CS2GameHelper.Core
{
    public static class ConcurrentQueueExtensions
    {
        public static List<T> DequeueBatch<T>(this ConcurrentQueue<T> queue, int count)
        {
            var list = new List<T>();
            for (int i = 0; i < count; i++)
            {
                if (!queue.TryDequeue(out T? item)) break;
                // item may be null for reference types; use null-forgiving since we successfully dequeued an element
                list.Add(item!);
            }
            return list;
        }
    }
}