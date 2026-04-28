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

## Setup: License Secrets

GitHub Actions runs on Ubuntu runners without a display, so Unity has to activate headlessly. `game-ci/unity-test-runner@v4` supports both license shapes; the workflow forwards both via `env:` and the action picks the path that has values.

**Personal license (free):** set three secrets — `UNITY_EMAIL`, `UNITY_PASSWORD`, `UNITY_SERIAL`. (Personal licenses do not produce a serial via the Hub UI; obtain one from `id.unity.com` if your account qualifies, or use the Pro/Plus path below.) `game-ci` activates with these and re-activates each run.

**Professional / Plus license (recommended for CI):**
1. Run [`game-ci/unity-request-activation-file`](https://game.ci/docs/github/activation) on a one-shot workflow to produce a `Unity_v*.alf` file from your repo's runner.
2. Sign the `.alf` at `license.unity3d.com` using your Unity account → download the resulting `.ulf` activation file.
3. Base64-encode the `.ulf` and store it as the `UNITY_LICENSE` repo secret:
   ```bash
   base64 -w 0 Unity_v6000.x.ulf > license.b64    # Linux
   base64 -i Unity_v6000.x.ulf -o license.b64     # macOS
   ```
4. The workflow reads `secrets.UNITY_LICENSE` into `env.UNITY_LICENSE`; the action decodes and activates Unity for every run.

The workflow's `env:` block exposes both shapes (`UNITY_LICENSE` for Pro/Plus, `UNITY_EMAIL`/`UNITY_PASSWORD`/`UNITY_SERIAL` for Personal). Set whichever set your license uses; leave the others unset.

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

**Add PlayMode tests:** create test suites in `Assets/_Project/Scripts/Tests/PlayMode/` and add a second test-runner step in `.github/workflows/tests.yml`:
```yaml
- uses: game-ci/unity-test-runner@v4
  with:
    projectPath: .
    unityVersion: 6000.3.11f1
    testMode: playmode
    artifactsPath: artifacts-playmode
    githubToken: ${{ secrets.GITHUB_TOKEN }}
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

**Why cache Library/?** Compiling the project from scratch takes 5-10 minutes. Caching compiled assemblies (in `Library/metadata` and `Library/ScriptAssemblies`) cuts this to 1-2 minutes. The cache key hashes `Assets/**`, `Packages/**`, and `ProjectSettings/**` — anything that changes the compiled output invalidates the cache. (Hashing `Library/**` itself would not work: `Library/` is gitignored and absent at checkout time, so the hash would be a constant and the cache would never invalidate.)

**Why game-ci/unity-test-runner?** It's the community standard for Unity CI, handles license activation, artifact upload, and result parsing. Rolling your own is error-prone.

**game-ci licensing model:** the tool itself is open-source (MIT). License activation is your responsibility (your license, not game-ci's). If you don't have a paid Unity license, contact Unity sales for a free CI license or use a smaller runner pool with a personal license (if supported in your region).
