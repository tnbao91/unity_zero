using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
using Unity.Notifications;
#endif

namespace Zero.Services.Notification
{
    /// <summary>
    /// Real notification service wrapping Unity Mobile Notifications package (Unified API).
    /// Cross-platform: uses NotificationCenter.ScheduleNotification + CancelScheduledNotification.
    /// Editor/Other: logs and no-ops (API stubs handle it).
    /// Permission outcome persisted via ISaveService (key: notification.permission.requested).
    /// </summary>
    public sealed class UnityMobileNotificationService : INotificationService
    {
        private readonly ILogService _log;
        private readonly ISaveService _saveService;
        private bool _permissionRequested;
        private readonly Dictionary<string, int> _scheduledIds = new();

        public UnityMobileNotificationService(ILogService log, ISaveService saveService)
        {
            _log = log;
            _saveService = saveService;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            // Initialize NotificationCenter (required before any other calls)
            var args = new NotificationCenterArgs()
            {
                AndroidChannelId = "default",
                PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge | NotificationPresentation.Sound,
            };
            NotificationCenter.Initialize(args);

            // Load persisted permission state
            if (_saveService.TryGet("notification.permission.requested", out bool requested))
            {
                _permissionRequested = requested;
            }

            _log.Info("[NOTIF] Initialized (unified cross-platform API)");
#else
            _log.Info("[NOTIF] no-op on unsupported platform");
#endif
            await UniTask.CompletedTask;
        }

        public async UniTask<bool> RequestPermissionAsync(CancellationToken ct = default)
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            // If already requested, return cached state
            if (_permissionRequested)
            {
                _log.Info("[NOTIF] Permission already requested (cached)");
                return true;
            }

            try
            {
                var permissionRequest = NotificationCenter.RequestPermission();
                await permissionRequest.ToUniTask(cancellationToken: ct);

                bool granted = permissionRequest.Status == NotificationsPermissionStatus.Granted;
                _permissionRequested = granted;
                _saveService.Set("notification.permission.requested", granted);
                _log.Info($"[NOTIF] Permission request completed (granted: {granted})");
                return granted;
            }
            catch (Exception ex)
            {
                _log.Error($"[NOTIF] Permission request failed: {ex.Message}");
                return false;
            }
#else
            _log.Info("[NOTIF] no-op on unsupported platform");
            _permissionRequested = true;
            return true;
#endif
        }

        public void Schedule(string id, string title, string body, TimeSpan delay)
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            try
            {
                var notification = new Unity.Notifications.Notification
                {
                    Title = title,
                    Text = body,
                    Data = id,
                };

                var schedule = new NotificationIntervalSchedule(delay, repeats: false);
                int numericId = NotificationCenter.ScheduleNotification(notification, schedule);

                // Cache the mapping so we can cancel by string id later
                _scheduledIds[id] = numericId;

                _log.Info($"[NOTIF] Scheduled '{id}' in {delay.TotalSeconds:F0}s (native id: {numericId})");
            }
            catch (Exception ex)
            {
                _log.Error($"[NOTIF] Schedule failed for '{id}': {ex.Message}");
            }
#else
            _log.Info("[NOTIF] no-op on unsupported platform");
#endif
        }

        public void Cancel(string id)
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            try
            {
                if (_scheduledIds.TryGetValue(id, out int numericId))
                {
                    NotificationCenter.CancelScheduledNotification(numericId);
                    _scheduledIds.Remove(id);
                    _log.Info($"[NOTIF] Cancelled '{id}' (native id: {numericId})");
                }
                else
                {
                    _log.Warn($"[NOTIF] Cancel called for unknown id '{id}'");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"[NOTIF] Cancel failed for '{id}': {ex.Message}");
            }
#else
            _log.Info("[NOTIF] no-op on unsupported platform");
#endif
        }

        public void CancelAll()
        {
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
            try
            {
                NotificationCenter.CancelAllScheduledNotifications();
                _scheduledIds.Clear();
                _log.Info("[NOTIF] All notifications cancelled");
            }
            catch (Exception ex)
            {
                _log.Error($"[NOTIF] CancelAll failed: {ex.Message}");
            }
#else
            _log.Info("[NOTIF] no-op on unsupported platform");
            _scheduledIds.Clear();
#endif
        }
    }
}
