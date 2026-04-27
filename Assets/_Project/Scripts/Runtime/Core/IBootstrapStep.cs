using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.Core
{
    public interface IBootstrapStep
    {
        string Name { get; }
        bool IsCritical { get; }
        UniTask ExecuteAsync(IProgress<float> progress, CancellationToken ct);
    }
}
