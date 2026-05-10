using UnityEngine;
using Zero.Core;

namespace Zero.Services.DeviceProfile
{
    public sealed class DeviceProfileService : IDeviceProfileService
    {
        private DeviceTier? _override;

        public IDeviceProfile Current { get; private set; }
        public DeviceTier DetectedTier { get; private set; }

        public DeviceProfileService()
        {
            DetectedTier = DetectInternal();
            Current = ProfileFor(DetectedTier);
        }

        public void Apply()
        {
            QualitySettings.globalTextureMipmapLimit = Current.TextureLimit;
            Application.targetFrameRate = Current.TargetFps;
            QualitySettings.shadows = Current.ShadowsEnabled ? ShadowQuality.HardOnly : ShadowQuality.Disable;
            QualitySettings.antiAliasing = Current.MsaaSampleCount;
        }

        public void Override(DeviceTier tier)
        {
            _override = tier;
            Current = ProfileFor(tier);
        }

        private DeviceTier DetectInternal()
        {
            if (_override.HasValue) return _override.Value;

            var ramMb = SystemInfo.systemMemorySize;
            if (ramMb < 3000) return DeviceTier.Low;
            if (ramMb < 6000) return DeviceTier.Mid;
            return DeviceTier.High;
        }

        private static IDeviceProfile ProfileFor(DeviceTier tier) => tier switch
        {
            DeviceTier.Low => DefaultDeviceProfile.Low(),
            DeviceTier.Mid => DefaultDeviceProfile.Mid(),
            DeviceTier.High => DefaultDeviceProfile.High(),
            _ => DefaultDeviceProfile.Mid()
        };
    }
}
