# CI — EditMode Tests and Automation

## Overview

The template ships with a GitHub Actions workflow (`.github/workflows/tests.yml`) that runs EditMode tests on every push and pull request. The workflow uses `game-ci/unity-test-runner@v4` to invoke Unity in headless mode, execute the test suite, and report results.

## CI Workflow

**Trigger:** any push to any branch, or a pull request.

**Steps:**
1. Check out code + LFS objects.
2. Cache `Library/` folder (packages, compiled assemblies) to speed up subsequent runs.
3. Run Unity in headless mode:
   ```
   Unity -batchmode -nographics -projectPath . \
     -runTests -testPlatform editmode \
     -testResults artifacts/results.xml
   ```
4. Upload test results as an artifact.

**Duration:** typically 10-15 minutes per run (first run without cache; ~3-5 min with cache).

## Setup: UNITY_LICENSE Secret

GitHub Actions runs on shared runners (Ubuntu Linux) without a display. To activate Unity in headless mode, you need a **personal or professional license activation file**.

**Steps:**

1. **Get a license:** 
   - Free Personal edition: download from `unity.com/download`; personal licenses CANNOT be used in CI (no headless activation).
   - **Professional / Paid edition:** sign in to `account.unity.com`, generate a license activation file.
   - **Evaluate:** contact Unity sales for a CI evaluation license (free for open-source projects).

2. **Activate on your machine:**
   - Install Unity 6.0.3.11f1.
   - Authenticate with your account: `Unity -quit -batchmode -username <email> -password <password> -serial <license>`.
   - Save the activation file (usually `~/.local/share/Unity/Unity_lic.ulf` on Linux).

3. **Convert to base64:**
   ```bash
   cat ~/.local/share/Unity/Unity_lic.ulf | base64 > license.txt
   cat license.txt
   ```

4. **Add to GitHub:**
   - Go to your repository → Settings → Secrets and variables → Actions.
   - Click "New repository secret".
   - Name: `UNITY_LICENSE`
   - Value: paste the base64-encoded license file content.

5. **Verify in workflow:**
   The workflow reads `env.UNITY_LICENSE` and passes it to `game-ci/unity-test-runner`, which decodes and activates Unity automatically.

## Interpreting Failures

**Test failure (red):**
```
Test(s) failed. Failures:
  MyTests.RoundTrip — AssertionException: Expected 42, got 41
```
→ Logic error in the test subject. Fix the code, push, and rerun.

**Compiler error (red):**
```
error CS0246: The type or namespace name 'Foo' could not be found
```
→ Missing reference in asmdef or syntax error. Fix and push.

**Timeout (red, 30min+):**
```
The job exceeded the maximum execution time
```
→ Test infinite-loops or is too slow. Add a timeout (`[Timeout(5000)]` in NUnit) or optimize the test.

**License activation failure (red):**
```
LICENSE ERROR: License activation failed
```
→ `UNITY_LICENSE` secret is invalid/expired or not set. Check GitHub Secrets and regenerate the license.

**Cache miss (yellow, slower run):**
→ First run or `Library/` was evicted. Expected; next run will be faster.

## Local Headless Testing

To test CI locally before pushing:

```bash
# Edit Mode tests
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform editmode \
  -testResults results.xml -quit

# Parse results
cat results.xml
```

If you see licensing errors locally, ensure `UNITY_LICENSE` env var is set:
```bash
export UNITY_LICENSE="$(cat /path/to/license/file.ulf)"
```

Then rerun the above.

## Extending CI

**Add PlayMode tests:** create test suites in `Assets/_Project/Scripts/Tests/PlayMode/` and update `.github/workflows/tests.yml`:
```yaml
- uses: game-ci/unity-test-runner@v4
  with:
    testPlatform: playmode  # Add a separate step for PlayMode
```

**Add code coverage:** add a post-step to upload coverage to Codecov:
```yaml
- uses: codecov/codecov-action@v3
  with:
    files: ./CodeCoverage/results.xml
```

**Custom test commands:** if you need pre/post-test setup (e.g., generate test fixtures), add a pre-build step:
```yaml
- run: |
    python3 scripts/generate_test_fixtures.py
```

## Known Limitations

- **Linux runners only:** game-ci uses Ubuntu runners. Tests must be platform-agnostic (no Mac-specific Input plugins, no Windows-only dependencies).
- **No Addressables remote:** if your game loads assets from a remote CDN, CI must either mock the fetch or pre-build Addressables locally before the test run.
- **No real device testing:** mobile Input, Notification, and Ads testing requires a real device or emulator. CI covers logic only; manual device testing is documented in `docs/testing/manual-checklist.md` (Phase 2).

## Design Rationale

**Why EditMode-only in CI?** Because PlayMode tests require rendering, Input System, loaded scenes, and often device-specific code (mobile Input, Notification). These are hard to mock comprehensively. Instead, EditMode tests cover service logic (Save, Pool, EventBus), and PlayMode/device testing is done manually (checklist in Phase 2).

**Why cache Library/?** Compiling the project from scratch takes 5-10 minutes. Caching compiled assemblies (in `Library/metadata` and `Library/ScriptAssemblies`) cuts this to 1-2 minutes. The cache key is a hash of `Library/**`, so a change to code invalidates the cache; dependencies are re-resolved.

**Why game-ci/unity-test-runner?** It's the community standard for Unity CI, handles license activation, artifact upload, and result parsing. Rolling your own is error-prone.

**game-ci licensing model:** the tool itself is open-source (MIT). License activation is your responsibility (your license, not game-ci's). If you don't have a paid Unity license, contact Unity sales for a free CI license or use a smaller runner pool with a personal license (if supported in your region).
