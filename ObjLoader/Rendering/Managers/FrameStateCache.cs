using ObjLoader.Rendering.Managers.Interfaces;

namespace ObjLoader.Rendering.Managers
{
    internal sealed class FrameStateCache : IFrameStateCache
    {
        private readonly Dictionary<long, FrameState> _cache = new();
        private const int MaxCacheSize = 600;
        private readonly Queue<long> _frameQueue = new();
        private readonly Queue<FrameState> _pool = new();
        private bool _isDisposed;

        public FrameState GetOrCreateState()
        {
            if (_isDisposed) return new FrameState();
            if (_pool.TryDequeue(out var state))
            {
                return state;
            }
            return new FrameState();
        }

        public void SaveState(long frame, FrameState state)
        {
            if (_isDisposed) return;
            if (_cache.TryAdd(frame, state))
            {
                _frameQueue.Enqueue(frame);
                while (_frameQueue.Count > MaxCacheSize)
                {
                    if (_frameQueue.TryDequeue(out var oldFrame))
                    {
                        if (_cache.Remove(oldFrame, out var oldState))
                        {
                            _pool.Enqueue(oldState);
                        }
                    }
                }
            }
            else
            {
                var replacedState = _cache[frame];
                if (replacedState != state)
                {
                    _pool.Enqueue(replacedState);
                }
                _cache[frame] = state;
            }
        }

        public bool TryGetState(long frame, out FrameState state)
        {
            if (_isDisposed)
            {
                state = default!;
                return false;
            }
            return _cache.TryGetValue(frame, out state!);
        }

        public void Clear()
        {
            foreach (var state in _cache.Values)
            {
                _pool.Enqueue(state);
            }
            _cache.Clear();
            _frameQueue.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Clear();
        }
    }
}