using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ObjLoader.Infrastructure
{
    internal sealed class ResourceAuditor : IDisposable
    {
        private static readonly Lazy<ResourceAuditor> _instance = new Lazy<ResourceAuditor>(() => new ResourceAuditor());
        private Timer? _auditTimer;
        private readonly object _timerLock = new();
        private int _disposed;
        private TimeSpan _auditInterval = TimeSpan.FromMinutes(5);
        private TimeSpan _leakThreshold = TimeSpan.FromMinutes(30);
        private volatile bool _isRunning;

        public static ResourceAuditor Instance => _instance.Value;

        public event Action<AuditReport>? AuditCompleted;

        private ResourceAuditor()
        {
        }

        private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public bool IsRunning => _isRunning;

        public void Start()
        {
            Start(_auditInterval);
        }

        public void Start(TimeSpan interval)
        {
            if (IsDisposed) return;
            if (interval.TotalMilliseconds < 100) interval = TimeSpan.FromMinutes(1);

            lock (_timerLock)
            {
                if (_isRunning && _auditInterval == interval) return;

                StopInternal();

                _auditInterval = interval;
                _auditTimer = new Timer(OnAuditTick, null, interval, interval);
                _isRunning = true;
            }
        }

        public void Restart(TimeSpan interval)
        {
            if (IsDisposed) return;
            if (interval.TotalMilliseconds < 100) interval = TimeSpan.FromMinutes(1);

            lock (_timerLock)
            {
                StopInternal();

                _auditInterval = interval;
                _auditTimer = new Timer(OnAuditTick, null, interval, interval);
                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (_timerLock)
            {
                StopInternal();
            }
        }

        private void StopInternal()
        {
            _isRunning = false;
            if (_auditTimer != null)
            {
                try
                {
                    _auditTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _auditTimer.Dispose();
                }
                catch
                {
                }
                _auditTimer = null;
            }
        }

        public void SetLeakThreshold(TimeSpan threshold)
        {
            if (threshold.TotalMilliseconds < 1000) threshold = TimeSpan.FromMinutes(1);
            _leakThreshold = threshold;
        }

        public AuditReport RunAudit()
        {
            if (IsDisposed) return AuditReport.Empty;

            try
            {
                var tracker = ResourceTracker.Instance;
                var stats = tracker.GetStats();
                var leaked = tracker.GetLeakedResources(_leakThreshold);
                var orphaned = tracker.GetOrphanedResources();

                var report = new AuditReport
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveResources = stats.ActiveResources,
                    OrphanedResources = stats.OrphanedResources + orphaned.Count,
                    TotalAllocations = stats.TotalAllocations,
                    TotalDisposals = stats.TotalDisposals,
                    EstimatedActiveBytes = stats.EstimatedActiveBytes,
                    LeakedResources = leaked,
                    OrphanedResourceList = orphaned
                };

                foreach (var leak in leaked)
                {
                    Debug.WriteLine(
                        $"[ResourceAuditor] Potential leak: Key={leak.Key}, Type={leak.ResourceType}, Age={leak.Age.TotalMinutes:F1}min, Size={leak.EstimatedSizeBytes}B");
                }

                foreach (var orphan in orphaned)
                {
                    Debug.WriteLine(
                        $"[ResourceAuditor] Orphaned: Key={orphan.Key}, Type={orphan.ResourceType}, CreatedAt={orphan.CreatedAt:O}");
                }

                if (leaked.Count > 0 || orphaned.Count > 0)
                {
                    Debug.WriteLine(
                        $"[ResourceAuditor] Summary: Active={report.ActiveResources}, Leaked={leaked.Count}, Orphaned={orphaned.Count}, TotalAlloc={report.TotalAllocations}, TotalDispose={report.TotalDisposals}, EstBytes={report.EstimatedActiveBytes}");
                }

                RaiseAuditCompleted(report);

                return report;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceAuditor] RunAudit failed: {ex.Message}");
                return AuditReport.Empty;
            }
        }

        private void RaiseAuditCompleted(AuditReport report)
        {
            var handler = AuditCompleted;
            if (handler == null) return;

            foreach (var d in handler.GetInvocationList())
            {
                try
                {
                    ((Action<AuditReport>)d)(report);
                }
                catch
                {
                }
            }
        }

        private void OnAuditTick(object? state)
        {
            if (IsDisposed || !_isRunning) return;

            try
            {
                RunAudit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ResourceAuditor] Audit tick failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Stop();
            AuditCompleted = null;
        }
    }

    internal sealed class AuditReport
    {
        public static readonly AuditReport Empty = new AuditReport();

        public DateTime Timestamp { get; set; }
        public int ActiveResources { get; set; }
        public int OrphanedResources { get; set; }
        public long TotalAllocations { get; set; }
        public long TotalDisposals { get; set; }
        public long EstimatedActiveBytes { get; set; }
        public List<ResourceAllocation> LeakedResources { get; set; } = new();
        public List<ResourceAllocation> OrphanedResourceList { get; set; } = new();

        public bool HasIssues => LeakedResources.Count > 0 || OrphanedResources > 0;
    }
}