using System.Windows.Threading;

namespace ObjLoader.Services.Camera
{
    public class CameraAnimationManager : IDisposable
    {
        private readonly DispatcherTimer _playbackTimer;
        private bool _isPlaying;

        public event EventHandler? Tick;

        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                }
            }
        }

        public CameraAnimationManager()
        {
            _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _playbackTimer.Tick += (s, e) => Tick?.Invoke(this, EventArgs.Empty);
        }

        public void Start()
        {
            if (_isPlaying) return;
            IsPlaying = true;
            _playbackTimer.Start();
        }

        public void Pause()
        {
            IsPlaying = false;
            _playbackTimer.Stop();
        }

        public void Stop()
        {
            IsPlaying = false;
            _playbackTimer.Stop();
        }

        public void Dispose()
        {
            _playbackTimer.Stop();
        }
    }
}