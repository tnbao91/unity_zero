using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public enum AudioBus
    {
        Master,
        Music,
        Sfx,
        Ui,
        Voice
    }

    public interface IAudioService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        UniTask PlayMusicAsync(string clipKey, bool loop = true, CancellationToken ct = default);
        void StopMusic();
        UniTask PlaySfxAsync(string clipKey, CancellationToken ct = default);
        void SetBusVolume(AudioBus bus, float volume);
        float GetBusVolume(AudioBus bus);
    }
}
