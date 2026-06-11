using Reflex.Core;
using Reflex.Enums;
using UnityEngine;
using Zero.Bootstrap.Steps;
using Zero.Core;
using Zero.Infrastructure;
using Zero.Services.AdPlacement;
using Zero.Services.Ads;
using Zero.Services.Analytics;
using Zero.Services.Asset;
using Zero.Services.Attribution;
using Zero.Services.Audio;
using Zero.Services.Consent;
using Zero.Services.Crashlytics;
using Zero.Services.DeviceProfile;
using Zero.Services.Events;
using Zero.Services.IAP;
using Zero.Services.Input;
using Zero.Services.Localization;
using Zero.Services.Log;
using Zero.Services.Notification;
using Zero.Services.Pool;
using Zero.Services.ReceiptValidator;
using Zero.Services.RemoteConfig;
using Zero.Services.Save;
using Zero.Services.Scene;
using Zero.Services.Time;
using Zero.Services.VersionCheck;
using Zero.Gameplay;
using Zero.UI;
using Resolution = Reflex.Enums.Resolution;

namespace Zero.Bootstrap
{
    // Marked partial for the template-CLONE workflow only: a fork can drop a
    // ProjectScopeInstaller.UserServices.cs next to this file. UPM consumers
    // CANNOT use it (C# partials never span assemblies) — their seams are their
    // own ContainerScope.OnRootContainerBuilding installer (bindings; last
    // registration wins) and BootstrapStepRegistration (pipeline steps).
    public static partial class ProjectScopeInstaller
    {
        // BeforeSplashScreen, not BeforeSceneLoad: Reflex resets the delegate at
        // AfterAssembliesLoaded, and consumer installers subscribe at
        // BeforeSceneLoad. Sitting between the two guarantees the template's
        // bindings always register FIRST, so a consumer's re-registration of the
        // same contract deterministically wins (Reflex resolves the LAST binding).
        // Cross-assembly ordering inside the same load type is unspecified — do
        // not move this back to BeforeSceneLoad.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Hook()
        {
            ContainerScope.OnRootContainerBuilding -= InstallBindings;
            ContainerScope.OnRootContainerBuilding += InstallBindings;
        }

        private static void InstallBindings(ContainerBuilder builder)
        {
            // Per-service installers — each service owns its own registration.
            LogServiceInstaller.Install(builder);
            EventsServiceInstaller.Install(builder);
            DeviceProfileServiceInstaller.Install(builder);
            CrashlyticsServiceInstaller.Install(builder);
            ConsentServiceInstaller.Install(builder);
            RemoteConfigServiceInstaller.Install(builder);
            AnalyticsServiceInstaller.Install(builder);
            LocalizationServiceInstaller.Install(builder);
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
            UIServiceInstaller.Install(builder);
            GameplayServiceInstaller.Install(builder);
            VersionCheckServiceInstaller.Install(builder);

            // Infrastructure-level singletons that don't have their own installer module.
            builder.RegisterType(
                typeof(BootstrapProgressReporter),
                new[] { typeof(IBootstrapProgressReporter) },
                Lifetime.Singleton,
                Resolution.Lazy);

            // Fork-mode hook only (see the partial note on the class). UPM
            // consumers extend via their own OnRootContainerBuilding installer.
            InstallUserBindings(builder);

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
                var l10n = c.Resolve<IL10nService>();
                var attrib = c.Resolve<IAttributionService>();
                var ads = c.Resolve<IAdsService>();
                var placement = c.Resolve<IAdPlacementService>();
                var iap = c.Resolve<IIAPService>();
                var save = c.Resolve<ISaveService>();
                var audio = c.Resolve<IAudioService>();
                var time = c.Resolve<ITimeService>();
                var notif = c.Resolve<INotificationService>();
                var versionCheck = c.Resolve<IVersionCheckService>();
                var reporter = c.Resolve<IBootstrapProgressReporter>();
                var bus = c.Resolve<IEventBus>();

                // Order: Crashlytics first (critical), Log/Profile next so subsequent steps
                // can log + read device info, Save moved up so any later step can read
                // persisted settings (audio volume, locale preference, consent state).
                // Localization sits next to Analytics so UI text is ready before Attribution
                // / Ads / IAP surface any localized errors. UIService has no bootstrap
                // step — consumers attach a UIRoot MonoBehaviour to their scene to wire
                // layer canvases at scene-load time.
                var defaultSteps = new IBootstrapStep[]
                {
                    new CrashlyticsStep(crash),
                    new LogStep(log),
                    new DeviceProfileStep(profile, log),
                    new SaveStep(save),
                    new AssetStep(asset),
                    new ConsentStep(consent),
                    new RemoteConfigStep(remote),
                    new AnalyticsStep(analytics),
                    new LocalizationStep(l10n, log),
                    new AttributionStep(attrib),
                    new AdsStep(ads, placement),
                    new IapStep(iap),
                    new AudioStep(audio),
                    new TimeStep(time),
                    new NotificationStep(notif),
                    new VersionCheckStep(versionCheck),
                };

                // Consumer seam: BootstrapStepRegistrations registered from a
                // consumer's own OnRootContainerBuilding installer are composed
                // onto the defaults (Append/Before/After/Replace by step name) —
                // see docs/architecture/bootstrap-pipeline.md.
                var steps = BootstrapStepComposer.Compose(
                    defaultSteps, c.All<BootstrapStepRegistration>());

                return new BootstrapPipeline(steps, log, reporter, bus);
            }, Lifetime.Singleton, Resolution.Lazy);
        }

        // Fork-mode seam: implementable only from inside this assembly (C#
        // partials cannot span assemblies). With no implementation the compiler
        // erases the call. UPM consumers: use OnRootContainerBuilding instead.
        static partial void InstallUserBindings(ContainerBuilder builder);
    }
}
