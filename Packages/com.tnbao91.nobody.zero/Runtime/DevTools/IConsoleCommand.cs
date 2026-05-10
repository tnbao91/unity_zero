using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.DevTools
{
    public interface IConsoleCommand
    {
        string Name { get; }
        string Help { get; }
        UniTask ExecuteAsync(string[] args, CancellationToken ct = default);
    }
}
