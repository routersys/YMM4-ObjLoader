using ObjLoader.Api.Core;
using ObjLoader.Plugin;
using System.Numerics;

namespace ObjLoader.Api.Camera
{
    internal sealed class CameraApi : ICameraApi
    {
        private readonly ObjLoaderParameter _parameter;
        private readonly List<WeakReference<Action<CameraState>>> _subscribers = new();
        private readonly object _subscriberLock = new();
        private CameraTransition? _activeTransition;
        private DateTime _transitionStartTime;

        internal CameraApi(ObjLoaderParameter parameter)
        {
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        }

        public CameraState GetState()
        {
            var pos = new Vector3(
                (float)_parameter.CameraX.GetValue(0, 1, 1),
                (float)_parameter.CameraY.GetValue(0, 1, 1),
                (float)_parameter.CameraZ.GetValue(0, 1, 1));
            var target = new Vector3(
                (float)_parameter.TargetX.GetValue(0, 1, 1),
                (float)_parameter.TargetY.GetValue(0, 1, 1),
                (float)_parameter.TargetZ.GetValue(0, 1, 1));
            var fov = (float)_parameter.Fov.GetValue(0, 1, 1);
            return new CameraState(pos, target, fov);
        }

        public void SetTransform(in Transform transform)
        {
            _parameter.CameraX.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.X, -100000, 100000));
            _parameter.CameraY.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.Y, -100000, 100000));
            _parameter.CameraZ.CopyFrom(new YukkuriMovieMaker.Commons.Animation(transform.Position.Z, -100000, 100000));
            NotifySubscribers(GetState());
        }

        public void RequestSmoothMove(in CameraTransition transition)
        {
            Interlocked.Exchange(ref _activeTransition, transition);
            _transitionStartTime = DateTime.UtcNow;
        }

        public IDisposable Subscribe(Action<CameraState> onChanged)
        {
            if (onChanged == null) throw new ArgumentNullException(nameof(onChanged));
            var weak = new WeakReference<Action<CameraState>>(onChanged);
            lock (_subscriberLock)
            {
                _subscribers.Add(weak);
            }
            return new Unsubscriber(() =>
            {
                lock (_subscriberLock)
                {
                    _subscribers.Remove(weak);
                }
            });
        }

        internal void Tick()
        {
            var transition = Volatile.Read(ref _activeTransition);
            if (transition == null) return;

            double elapsed = (DateTime.UtcNow - _transitionStartTime).TotalSeconds;
            double total = transition.Duration.TotalSeconds;
            if (total <= 0)
            {
                SetTransform(transition.Target);
                Interlocked.Exchange(ref _activeTransition, null);
                return;
            }

            double t = Math.Clamp(elapsed / total, 0.0, 1.0);
            double easedT = ApplyEasing(t, transition.Easing);

            var from = GetState();
            var toPos = transition.Target.Position;
            var lerped = new Transform(
                Vector3.Lerp(from.Position, toPos, (float)easedT),
                transition.Target.RotationEulerDegrees,
                transition.Target.Scale);
            SetTransform(lerped);

            if (t >= 1.0)
                Interlocked.Exchange(ref _activeTransition, null);
        }

        private static double ApplyEasing(double t, EasingType easing) => easing switch
        {
            EasingType.EaseIn => t * t,
            EasingType.EaseOut => t * (2.0 - t),
            EasingType.EaseInOut => t < 0.5 ? 2.0 * t * t : -1.0 + (4.0 - 2.0 * t) * t,
            _ => t
        };

        private void NotifySubscribers(CameraState state)
        {
            List<WeakReference<Action<CameraState>>> snapshot;
            lock (_subscriberLock)
            {
                snapshot = new List<WeakReference<Action<CameraState>>>(_subscribers);
            }

            var dead = new List<WeakReference<Action<CameraState>>>();
            foreach (var weak in snapshot)
            {
                if (weak.TryGetTarget(out var cb))
                    cb(state);
                else
                    dead.Add(weak);
            }

            if (dead.Count > 0)
            {
                lock (_subscriberLock)
                {
                    foreach (var d in dead)
                        _subscribers.Remove(d);
                }
            }
        }

        private sealed class Unsubscriber : IDisposable
        {
            private Action? _action;
            internal Unsubscriber(Action action) { _action = action; }
            public void Dispose() { Interlocked.Exchange(ref _action, null)?.Invoke(); }
        }
    }
}