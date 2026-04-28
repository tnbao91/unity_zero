# Writing EditMode Tests

## Overview

EditMode tests run in the Unity Editor without starting Play Mode. They verify logic without needing a loaded scene, rendering, or Input System. All tests in `Assets/_Project/Scripts/Tests/EditMode/` are gated by the `UNITY_INCLUDE_TESTS` define and only compile in the Editor.

## Test Setup

**Asmdef:** `Assets/_Project/Scripts/Tests/EditMode/Zero.Tests.EditMode.asmdef` references:
- `UnityEngine.TestRunner` (test runner infrastructure)
- `UnityEditor.TestRunner` (editor-specific test utilities)
- `Zero.Core`, `Zero.Infrastructure` (interfaces and base classes)
- Service asmdefs needed by your tests (`Zero.Services.Save`, `Zero.Services.Pool`, etc.)
- `UniTask`, `R3`, `Reflex` (testing reactive/async code)
- NUnit framework (`nunit.framework.dll` in precompiled references)

**Test fixture:**
```csharp
using NUnit.Framework;

[TestFixture]
public sealed class MyServiceTests
{
    private MyService _service;

    [SetUp]
    public void SetUp()
    {
        // Run before each test
        _service = new MyService();
    }

    [TearDown]
    public void TearDown()
    {
        // Run after each test; cleanup resources
        _service?.Dispose();
    }

    [Test]
    public void SynchronousTest()
    {
        // Pure synchronous assertion
        var result = _service.ComputeValue();
        Assert.AreEqual(expected, result);
    }
}
```

## Async Test Patterns

**UniTask async tests:**
```csharp
[UnityTest]
public IEnumerator AsyncTest() => UniTask.ToCoroutine(async () =>
{
    // Await UniTask directly
    await _service.LoadAsync();
    Assert.IsTrue(_service.IsLoaded);
});
```

**CancellationToken injection:**
```csharp
[UnityTest]
public IEnumerator CancelledOperationThrows() => UniTask.ToCoroutine(async () =>
{
    var cts = new CancellationTokenSource();
    cts.Cancel();
    
    var ex = Assert.ThrowsAsync<OperationCanceledException>(async () =>
        await _service.LongRunningAsync(cts.Token));
    
    Assert.NotNull(ex);
});
```

## Mocking and Stubs

**Stub for a dependency** — match the actual `ILogService` shape exactly (`IsEnabled`, `Info`, `Warn`, `Error(string)`, `Error(Exception, string=null)` — there is no `Debug`):
```csharp
private sealed class StubLogService : ILogService
{
    public bool IsEnabled { get; set; } = true;
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
    public void Error(Exception exception, string context = null) { }
}

// Usage in test
var service = new MyService(new StubLogService());
```

**Spy (track calls):**
```csharp
private sealed class SpyLogService : ILogService
{
    public bool IsEnabled { get; set; } = true;
    public int ErrorCallCount { get; private set; }

    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) => ErrorCallCount++;
    public void Error(Exception exception, string context = null) => ErrorCallCount++;
}

[Test]
public void ErrorLogged()
{
    var log = new SpyLogService();
    var service = new MyService(log);

    service.TriggerError();

    Assert.AreEqual(1, log.ErrorCallCount);
}
```

## Common Assertions

```csharp
// Basic
Assert.AreEqual(expected, actual);
Assert.IsTrue(condition);
Assert.IsFalse(condition);
Assert.IsNull(reference);
Assert.IsNotNull(reference);
Assert.AreSame(obj1, obj2);  // Reference equality
Assert.AreNotSame(obj1, obj2);

// Collections
Assert.AreEqual(new[] { 1, 2, 3 }, actualArray);
Assert.Contains("needle", collection);
Assert.That(actualList, Has.Count.EqualTo(3));

// String matching
Assert.That(message, Does.Contain("error"));
Assert.That(message, Does.StartWith("Loading"));

// Exception testing
Assert.Throws<InvalidOperationException>(() => service.BadCall());
var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => 
    await service.BadCallAsync());
Assert.That(ex.Message, Does.Contain("expected error text"));
```

## Examples

**Round-trip test (Save service):**
```csharp
[UnityTest]
public IEnumerator RoundTrip() => UniTask.ToCoroutine(async () =>
{
    var service1 = new EncryptedJsonSaveService(new StubLogService());
    await service1.LoadAsync();
    
    service1.Set("player_name", "Alice");
    service1.Set("level", 42);
    await service1.SaveAsync();
    
    var service2 = new EncryptedJsonSaveService(new StubLogService());
    await service2.LoadAsync();
    
    Assert.IsTrue(service2.TryGet("player_name", out string name));
    Assert.AreEqual("Alice", name);
    Assert.IsTrue(service2.TryGet("level", out int level));
    Assert.AreEqual(42, level);
});
```

**Observable subscription test (Event bus):**
```csharp
[Test]
public void PublishSubscribe()
{
    var bus = new R3EventBus();
    int received = 0;
    
    using var sub = bus.On<int>().Subscribe(v => received = v);
    
    bus.Publish(42);
    
    Assert.AreEqual(42, received);
}
```

## Known Limitations

- **No scene loading:** EditMode tests run without a scene. If your code depends on `GameObject.Find()` or scene hierarchy, you must stub those or create minimal GameObjects programmatically.
- **No Input System:** Input injection (new Input System) doesn't work in EditMode. Test input handling by mocking `IInputService` instead.
- **No rendering:** no cameras, no rendered output. UI tests must be logic-only (event firing, data binding); visual verification requires PlayMode or manual editor testing.
- **No Addressables:** if your code loads assets from Addressables, you must mock `IAssetService` in tests. Full Addressables round-trip testing requires PlayMode.

## Running Tests

**In the Editor:** `Window → General → Test Runner`, switch to **EditMode**, click Run.

**Headless (CI):**
```bash
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform editmode \
  -testResults results.xml -quit
```

## Design Rationale

**Why EditMode instead of PlayMode for CI?** PlayMode tests start the full game loop, load scenes, instantiate GameObjects, and require graphics initialization. On a headless CI runner, this adds complexity (faking rendering, managing scene lifecycle). EditMode tests are lightweight: they test logic in isolation and run in seconds. PlayMode testing is valuable for integration (Input → UI → Game state), but is best done manually (see `docs/testing/manual-checklist.md` for Phase 2).

**Async pattern with `UniTask.ToCoroutine`:** Unity's IEnumerator is the native async model for editor tests. We wrap UniTask async/await code by converting it to an IEnumerator via `.ToCoroutine()`. This integrates cleanly with the test runner's coroutine scheduling.

**Stub over mock frameworks:** the template doesn't pull in Moq or similar. Simple nested stubs are more transparent, less magic, and avoid a third-party test dependency. For complex mocking needs, consumers can add Moq themselves.
