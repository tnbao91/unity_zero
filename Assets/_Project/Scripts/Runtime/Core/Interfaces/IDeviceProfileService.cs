namespace Zero.Core
{
    public enum DeviceTier
    {
        Low,
        Mid,
        High
    }

    public interface IDeviceProfile
    {
        DeviceTier Tier { get; }
        int TargetFps { get; }
        int TextureLimit { get; }
        bool ShadowsEnabled { get; }
        int MsaaSampleCount { get; }
        bool PostProcessingEnabled { get; }
    }

    public interface IDeviceProfileService
    {
        IDeviceProfile Current { get; }
        DeviceTier DetectedTier { get; }
        void Apply();
        void Override(DeviceTier tier);
    }
}
