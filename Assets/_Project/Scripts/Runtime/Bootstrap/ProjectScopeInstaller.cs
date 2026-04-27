using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using Zero.Bootstrap.Steps;
using Zero.Core;
using Zero.Services.AdPlacement;
using Zero.Services.Ads;
using Zero.Services.Analytics;
using Zero.Services.Asset;
using Zero.Services.Attribution;
using Zero.Services.Audio;
using Zero.Services.Consent;
using Zero.Services.Crashlytics;
using Zero.Services.DeviceProfile;
using Zero.Services.IAP;
using Zero.Services.Input;
using Zero.Services.Log;
using Zero.Services.Notification;
using Zero.Services.Pool;
using Zero.Services.ReceiptValidator;
using Zero.Services.RemoteConfig;
using Zero.Services.Save;
using Zero.Services.Scene;
using Zero.Services.Time;
using Resolution = Reflex.Enums.Resolution;

namespace Zero.Bootstrap
{
    internal static class ProjectScopeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            ContainerScope.OnRootContainerBuilding -= InstallBindings;
            ContainerScope.OnRootContainerBuilding += InstallBindings;
        }

        private static void InstallBindings(ContainerBuilder builder)
        {
            // Per-service installers — each service owns its own registration.
            LogServiceInstaller.Install(builder);
            DeviceProfileServiceInstaller.Install(builder);
            CrashlyticsServiceInstaller.Install(builder);
            ConsentServiceInstaller.Install(builder);
            RemoteConfigServiceInstaller.Install(builder);
            AnalyticsServiceInstaller.Install(builder);
            SaveServiceInstaller.Install(builder);
            AssetServiceInstaller.Install(builder);
            SceneServiceInstaller.Install(builder);
            AttributionServiceInstaller.Install(builder);
            AdsServiceInstaller.Install(builder);
            AdPlacementServiceInstaller.Install(builder);
            ReceiptValidatorServiceInstaller.Install(builder);
            IapServiceInstaller.Install(builder);
            AudioServiceInstaller.Install(builder);
            TimeServiceInstaller.Install(builder);
            InputServiceInstaller.Install(builder);
            NotificationServiceInstaller.Install(builder);
            PoolServiceInstaller.Install(builder);

            // Pipeline factory — explicit step list keeps ordering deterministic.
            builder.RegisterFactory(c =>
            {
                var log = c.Resolve<ILogService>();
                var crash = c.Resolve<ICrashlyticsService>();
                var profile = c.Resolve<IDeviceProfileService>();
                var asset = c.Resolve<IAssetService>();
                var consent = c.Resolve<IConsentService>();
                var remote = c.Resolve<IRemoteConfigService>();
                var analytics = c.Resolve<IAnalyticsService>();
                var attrib = c.Resolve<IAttributionService>();
                var ads = c.Resolve<IAdsService>();
                var placement = c.Resolve<IAdPlacementService>();
                var iap = c.Resolve<IIAPService>();
                var save = c.Resolve<ISaveService>();
                var audio = c.Resolve<IAudioService>();
                var time = c.Resolve<ITimeService>();
                var notif = c.Resolve<INotificationService>();

                var steps = new IBootstrapStep[]
                {
                    new CrashlyticsStep(crash),
                    new LogStep(log),
                    new DeviceProfileStep(profile, log),
                    new AssetStep(asset),
                    new ConsentStep(consent),
                    new RemoteConfigStep(remote),
                    new AnalyticsStep(analytics),
                    new AttributionStep(attrib),
                    new AdsStep(ads, placement),
                    new IapStep(iap),
                    new AudioStep(audio),
                    new TimeStep(time),
                    new NotificationStep(notif),
                    new SaveStep(save),
                };
                return new BootstrapPipeline(steps, log);
            }, Lifetime.Singleton, Resolution.Lazy);
        }
    }
}
