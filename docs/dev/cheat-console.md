# Cheat Console

## Overview

`CheatConsole` is an in-game IMGUI command line for development builds. Toggle with the tilde key (` ` `) on desktop or a 4-finger tap on mobile. Type a command and hit Execute. Discovers commands by reflection scanning all loaded assemblies for `[ConsoleCommand]`-decorated `IConsoleCommand` impls. Lives in `Zero.DevTools` which is gated behind `defineConstraints: ["UNITY_EDITOR || DEVELOPMENT_BUILD"]` — production builds strip the assembly entirely.

The console is spawned once at `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` by `DevToolsBootstrap`, attached to a `[Zero.DevTools]` GameObject marked `DontDestroyOnLoad`. No prefab, no scene authoring needed.

## Public API

```csharp
namespace Zero.DevTools
{
    public interface IConsoleCommand
    {
        string Name { get; }   // e.g. "save reset" — single or multi-word
        string Help { get; }
        UniTask ExecuteAsync(string[] args, CancellationToken ct = default);
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConsoleCommandAttribute : Attribute
    {
        public string Group { get; }
        public string Help { get; }
        public ConsoleCommandAttribute(string group, string help);
    }
}
```

A command is any `class` that:
1. Implements `IConsoleCommand`.
2. Carries `[ConsoleCommand("group", "help")]`.
3. Lives in any asmdef the runtime can scan (DevTools or any asmdef referenced by the running app).

## Built-in commands

| Command | What it does |
|---|---|
| `loc set <locale>` | Calls `IL10nService.SetLocaleAsync(args[0])`. E.g. `loc set vi-VN`. |
| `version check` | Calls `IVersionCheckService.CheckAsync` and prints `Status / LocalVersion / RemoteMinVersion`. |
| `fps show` / `fps hide` | Toggles the [FPS overlay](fps-overlay.md). |
| `save reset` | **Stub.** ISaveService has `Delete(key)` but no wholesale reset by design. Logs a "extend per-game" warn. Override per-game by writing your own command (see [Examples](#examples)). |

## Extension Points

### Add a command

Drop a class anywhere in your game asmdef:

```csharp
using Cysharp.Threading.Tasks;
using System.Threading;
using Zero.Core;
using Zero.DevTools;

[ConsoleCommand("wallet", "Wallet manipulation")]
public sealed class WalletAddCommand : IConsoleCommand
{
    private readonly IWalletService _wallet;
    private readonly ILogService _log;

    public string Name => "wallet add";
    public string Help => "wallet add <currency> <amount>";

    public WalletAddCommand(IWalletService wallet, ILogService log)
    {
        _wallet = wallet;
        _log = log;
    }

    public UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            _log.Warn("[Console] " + Help);
            return UniTask.CompletedTask;
        }
        if (!int.TryParse(args[1], out var amount))
        {
            _log.Warn("[Console] Amount must be an integer.");
            return UniTask.CompletedTask;
        }
        _wallet.Add(args[0], amount);
        _log.Info($"[Console] Added {amount} {args[0]}");
        return UniTask.CompletedTask;
    }
}
```

Constructor params are resolved through Reflex (`Container.RootContainer.Construct(Type)`) — anything you've registered as a contract (`IWalletService`, `ILogService` etc.) is injected automatically. Commands without registered deps fall back to `Activator.CreateInstance`.

### Override `save reset`

Because `ISaveService` has no wholesale reset, the built-in command is a stub. Override:

```csharp
[ConsoleCommand("save", "Save manipulation")]
public sealed class GameSaveResetCommand : IConsoleCommand
{
    private readonly ISaveService _save;
    public string Name => "save reset";
    public string Help => "Wipe all save keys this game writes";

    public GameSaveResetCommand(ISaveService save) { _save = save; }

    public UniTask ExecuteAsync(string[] args, CancellationToken ct = default)
    {
        // List every key your game writes. ISaveService doesn't expose
        // GetAllKeys by design — be explicit.
        _save.Delete("wallet.coins");
        _save.Delete("wallet.gems");
        _save.Delete("progression.last_level");
        // ... etc.
        return _save.SaveAsync(ct);
    }
}
```

The discovery scan picks up *both* commands but the dictionary keys by `Name` — your override replaces the stub. (If both register the same `Name`, last-write-wins; the order is `AppDomain.CurrentDomain.GetAssemblies()` order which is not deterministic across editor restarts, so prefer disambiguating names like `save reset` (yours) vs not registering the stub by removing `BuiltInCommands.SaveResetCommand`.)

### Disable in DEVELOPMENT_BUILD

`Zero.DevTools` only compiles when `UNITY_EDITOR || DEVELOPMENT_BUILD`. To strip it from a development build (e.g. an internal QA build that should not have a console), edit `Zero.DevTools.asmdef` and set `defineConstraints: ["UNITY_EDITOR"]` instead — DevTools compiles only in the Editor.

## Examples

Open the console in Play mode, type:

```
loc set vi-VN
version check
fps show
save reset
```

Each command writes to the in-overlay log + (typically) `_log.Info` so it also appears in the Unity Console.

## Known Limitations

- **IMGUI only.** No uGUI or UIToolkit version. The console intentionally has zero prefab dependency — a uGUI version would require Reflex + scene wiring, defeating the spawn-anywhere promise.
- **No history / autocomplete.** A simple text field. Add via consumer fork if you need recall.
- **4-finger tap is "any 4 fingers begin in same frame".** Some devices may not register simultaneously; tilde via `Bluetooth keyboard` is the reliable mobile fallback.
- **Commands run on the Unity main thread.** `ExecuteAsync` is awaited inside the Update loop via `UniTaskVoid.Forget()` — no thread juggling, but a long-running command blocks the next input for that frame.
- **`Container.RootContainer` is grabbed at command-construct time.** If your command resolves a service that's only registered in a sub-scope (not the root), the construct fails and falls back to `Activator.CreateInstance` — which throws if the ctor isn't parameterless.
- **Production builds have no console at all** (asmdef strips). To debug a release build you must produce a DEVELOPMENT_BUILD.

## Design Rationale

- **`[ConsoleCommand]` reflection scan over manual registration.** Manual registration would require either a central registry consumers fork (template smell) or a per-game `RegisterAll()` call. Reflection is paid once at `Start` and decouples the console from per-game wiring.
- **`Container.Construct` not `Container.Resolve`.** Commands aren't registered as Reflex contracts — they're discovered dynamically and built once per session. `Construct` does ctor injection without registration; `Resolve` would throw `UnknownContractException`.
- **Greedy two-word matching.** `save reset` has to be one command name (not `save` with `reset` as arg) so commands can group by namespace (`save *`, `wallet *`, `level *`). Parser tries 2-word first, falls back to 1-word.
- **No prefab / no scene placement.** The console is dev-only — making consumers author a UI prefab they'd never touch in production is friction without value.
- **Asmdef-level `defineConstraints` over per-file `#if`.** Stronger isolation: the assembly is *literally absent* from production builds, so reflection (e.g. third-party "find all types") cannot rediscover commands. Belt-and-suspenders matching `[RuntimeInitializeOnLoadMethod]`'s own `#if`.
