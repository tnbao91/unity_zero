using Zero.Core;

namespace Zero.Services.DeviceProfile
{
    public sealed class DefaultDeviceProfile : IDeviceProfile
    {
        public DefaultDeviceProfile(DeviceTier tier, int targetFps, int textureLimit, bool shadowsEnabled, int msaaSampleCount, bool postProcessingEnabled)
        {
            Tier = tier;
            TargetFps = targetFps;
            TextureLimit = textureLimit;
            ShadowsEnabled = shadowsEnabled;
            MsaaSampleCount = msaaSampleCount;
            PostProcessingEnabled = postProcessingEnabled;
        }

        public DeviceTier Tier { get; }
        public int TargetFps { get; }
        public int TextureLimit { get; }
        public bool ShadowsEnabled { get; }
        public int MsaaSampleCount { get; }
        public bool PostProcessingEnabled { get; }

        public static DefaultDeviceProfile Low() => new(DeviceTier.Low, 30, 1, false, 0, false);
        public static DefaultDeviceProfile Mid() => new(DeviceTier.Mid, 60, 0, true, 2, true);
        public static DefaultDeviceProfile High() => new(DeviceTier.High, 60, 0, true, 4, true);
    }
}
