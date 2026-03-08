namespace ObjLoader.Api.Core
{
    public static class SceneContext
    {
        private static readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
        private static readonly Dictionary<string, (ISceneServices Services, Guid Token)> _registry = new();

        public static event EventHandler<SceneRegistrationChangedEventArgs>? RegistrationChanged;

        public static Guid Register(string instanceId, ISceneServices services)
        {
            if (string.IsNullOrEmpty(instanceId)) throw new ArgumentNullException(nameof(instanceId));
            if (services == null) throw new ArgumentNullException(nameof(services));

            var token = Guid.NewGuid();

            _lock.EnterWriteLock();
            try
            {
                _registry[instanceId] = (services, token);
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            RegistrationChanged?.Invoke(null, new SceneRegistrationChangedEventArgs(instanceId, true));
            return token;
        }

        public static bool Unregister(string instanceId, Guid token)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;

            bool removed;
            _lock.EnterWriteLock();
            try
            {
                if (_registry.TryGetValue(instanceId, out var entry) && entry.Token == token)
                {
                    _registry.Remove(instanceId);
                    removed = true;
                }
                else
                {
                    removed = false;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            if (removed)
                RegistrationChanged?.Invoke(null, new SceneRegistrationChangedEventArgs(instanceId, false));

            return removed;
        }

        public static bool TryGetScene(string instanceId, out ISceneServices? services)
        {
            _lock.EnterReadLock();
            try
            {
                if (_registry.TryGetValue(instanceId, out var entry))
                {
                    services = entry.Services;
                    return true;
                }
                services = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public static IReadOnlyList<string> GetAllSceneIds()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<string>(_registry.Keys);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public static ISceneServices? GetFirstScene()
        {
            _lock.EnterReadLock();
            try
            {
                foreach (var entry in _registry.Values)
                {
                    return entry.Services;
                }
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}