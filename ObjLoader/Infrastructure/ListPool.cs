namespace ObjLoader.Infrastructure
{
    internal static class ListPool<T>
    {
        private static readonly ObjectPool<List<T>> _pool = new ObjectPool<List<T>>(
            () => new List<T>(),
            list => list.Clear(),
            32);

        public static List<T> Rent()
        {
            return _pool.Rent();
        }

        public static void Return(List<T> list)
        {
            if (list == null) return;
            if (list.Capacity > 4096)
            {
                list.Clear();
                return;
            }
            _pool.Return(list);
        }
    }

    internal static class DictionaryPool<TKey, TValue> where TKey : notnull
    {
        private static readonly ObjectPool<Dictionary<TKey, TValue>> _pool = new ObjectPool<Dictionary<TKey, TValue>>(
            () => new Dictionary<TKey, TValue>(),
            dict => dict.Clear(),
            32);

        public static Dictionary<TKey, TValue> Rent()
        {
            return _pool.Rent();
        }

        public static void Return(Dictionary<TKey, TValue> dict)
        {
            if (dict == null) return;
            if (dict.Count > 4096)
            {
                dict.Clear();
                return;
            }
            _pool.Return(dict);
        }
    }

    internal static class HashSetPool<T>
    {
        private static readonly ObjectPool<HashSet<T>> _pool = new ObjectPool<HashSet<T>>(
            () => new HashSet<T>(),
            set => set.Clear(),
            32);

        public static HashSet<T> Rent()
        {
            return _pool.Rent();
        }

        public static void Return(HashSet<T> set)
        {
            if (set == null) return;
            if (set.Count > 4096)
            {
                set.Clear();
                return;
            }
            _pool.Return(set);
        }
    }
}