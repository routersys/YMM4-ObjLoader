using System.Collections.Concurrent;

namespace ObjLoader.Infrastructure
{
    internal sealed class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T>? _reset;
        private readonly int _maxSize;
        private int _count;

        public ObjectPool(Func<T> factory, Action<T>? reset = null, int maxSize = 64)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _reset = reset;
            _maxSize = Math.Max(1, maxSize);
            _pool = new ConcurrentBag<T>();
        }

        public T Rent()
        {
            if (_pool.TryTake(out var item))
            {
                Interlocked.Decrement(ref _count);
                return item;
            }
            return _factory();
        }

        public void Return(T item)
        {
            if (item == null) return;
            if (Volatile.Read(ref _count) >= _maxSize) return;

            try
            {
                _reset?.Invoke(item);
            }
            catch
            {
                return;
            }

            _pool.Add(item);
            Interlocked.Increment(ref _count);
        }

        public void Clear()
        {
            while (_pool.TryTake(out _))
            {
                Interlocked.Decrement(ref _count);
            }
        }
    }
}