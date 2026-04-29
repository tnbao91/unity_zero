using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zero.Core;

#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace Zero.Services.Notification
{
    /// <summary>
    /// Real notification service wrapping Unity Mobile Notifications package.
    /// iOS: uses iOSNotificationCenter with permission request.
    /// Android: uses AndroidNotificationCenter with default channel.
    /// Editor/Other: logs and no-ops.
    /// Permission outcome persisted via ISaveService (key: notification.permission.requested).
    /// </summary>
    public sealed class UnityMobileNotificationService : INotificationService
    {
        private readonly ILogService _log;
        private readonly ISaveService _saveService;
        private bool _permissionRequested;

        public UnityMobileNotificationService(ILogService log, ISaveService saveService)
        {
            _log = log;
            _saveService = saveService;
        }

        public async UniTask InitializeAsync(CancellationToken ct = default)
        {
#if UNITY_ANDROID
            // Register default notification channel on Android
            var channel = new AndroidNotificationChannel
            {
                Id = "default",
                Name = "Default",
                Description = "Generic notifications",
                Importance = Importance.Default,
                CanBypassDnd = false,
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
            _log.Info("[NOTIF] Android channel 'default' registered");
#elif UNITY_IOS
            _log.Info("[NOTIF] iOS notification center initialized");
#else
            _log.Info("[NOTIF] Notifications not supported on this platform");
#endif

            // Load persisted permission state
            if (_saveService.TryGet("notification.permission.requested", out bool requested))
            {
                _permissionRequested = requested;
            }

            await UniTask.CompletedTask;
        }

        public async UniTask<bool> RequestPermissionAsync(CancellationToken ct = default)
        {
            // If already requested, return cached state
            if (_permissionRequested)
            {
                _log.Info("[NOTIF] Permission already requested (cached)");
                return true;
            }

#if UNITY_IOS
            var request = new GameNotificationRequest()
            {
                Identifier = 0,
                Title = "",
                Text = "",
                FireTime = System.DateTime.Now.AddSeconds(1),
            };

            try
            {
                iOSNotificationCenter.SendNotification(request, "default");
                _log.Info("[NOTIF] iOS permission requested");
                _permissionRequested = true;
                _saveService.Set("notification.permission.requested", true);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error($"[NOTIF] iOS permission request failed: {ex.Message}");
                return false;
            }
#elif UNITY_ANDROID
            _log.Info("[NOTIF] Android notifications auto-permitted");
            _permissionRequested = true;
            _saveService.Set("notification.permission.requested", true);
            return true;
#else
            _log.Info("[NOTIF] Permission request no-op on non-mobile platform");
            _permissionRequested = true;
            _saveService.Set("notification.permission.requested", true);
            return true;
#endif
        }

        public void Schedule(string id, string title, string body, TimeSpan delay)
        {
#if UNITY_IOS
            var notification = new GameNotification
            {
                Identifier = int.TryParse(id, out int numId) ? numId : id.GetHashCode(),
                Title = title,
                Body = body,
                FireTime = System.DateTime.Now.Add(delay),
            };
            iOSNotificationCenter.SendNotification(notification, "default");
            _log.Info($"[NOTIF:iOS] Scheduled '{id}' in {delay.TotalSeconds:F0}s");
#elif UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = System.DateTime.Now.Add(delay),
                IntentData = id,
            };
            AndroidNotificationCenter.SendNotification(notification, "default");
            _log.Info($"[NOTIF:Android] Scheduled '{id}' in {delay.TotalSeconds:F0}s");
#else
            _log.Info($"[NOTIF] Schedule no-op on non-mobile: '{id}' (title: {title}, delay: {delay.TotalSeconds:F0}s)");
#endif
        }

        public void Cancel(string id)
        {
#if UNITY_IOS
            if (int.TryParse(id, out int numId))
            {
                iOSNotificationCenter.RemoveNotification(numId);
                _log.Info($"[NOTIF:iOS] Cancelled '{id}'");
            }
#elif UNITY_ANDROID
            AndroidNotificationCenter.CancelNotification(id);
            _log.Info($"[NOTIF:Android] Cancelled '{id}'");
#else
            _log.Info($"[NOTIF] Cancel no-op on non-mobile: '{id}'");
#endif
        }

        public void CancelAll()
        {
#if UNITY_IOS
            iOSNotificationCenter.ClearNotifications();
            _log.Info("[NOTIF:iOS] All notifications cancelled");
#elif UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
            _log.Info("[NOTIF:Android] All notifications cancelled");
#else
            _log.Info("[NOTIF] CancelAll no-op on non-mobile");
#endif
        }
    }
}
