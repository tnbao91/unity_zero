# Unity Zero — Template game hybrid casual

Template Unity 6 LTS opensource cho hybrid casual và puzzle games. Hạ tầng tối giản, production-ready, có extension points để build prototype trong vài ngày. Meta loop (wallet / progression / rewards) **không** ship trong template — xem `docs/meta/recipes.md` cho per-game patterns.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity 6.0.3.11f1](https://img.shields.io/badge/Unity-6.0.3.11f1-black.svg)](ProjectSettings/ProjectVersion.txt)

> Phiên bản tiếng Anh đầy đủ: [README.md](README.md). File này chỉ pitch + Quick Start.

## Stack đã chốt (không thay)

Reflex (DI) · UniTask (async) · R3 (reactive) · LitMotion (tween) · Newtonsoft.Json + ZString · `com.unity.localization` · `com.unity.mobile.notifications` · `UnityEngine.Pool.ObjectPool`.

Lý do từng pick: xem `Stack` table trong [README.md](README.md#stack-locked-do-not-substitute).

## Quick Start

1. **Clone** repo.
2. **Mở project** với Unity 6.0.3.11f1 (đúng version trong `ProjectSettings/ProjectVersion.txt`). Đừng để Unity upgrade sang LTS khác.
3. **Restore NuGet** nếu thiếu Newtonsoft.Json hoặc các transitive deps của R3 — `NuGet → Restore Packages`. Repo có patched `.meta` files cho R3 + deps để bật `Editor.enabled`; đừng để NuGet revert.
4. **Tạo encryption seed asset.** Copy `Assets/Resources/ZeroSecrets.asset.example` → `Assets/Resources/ZeroSecrets.asset`, mở trong Inspector và thay các string `REPLACE_ME_*` bằng random bytes. File mới được gitignored. Player build sẽ throw nếu thiếu hoặc còn placeholder. Chi tiết: [docs/security/save-encryption.md](docs/security/save-encryption.md).
5. **Mở `Assets/_Project/Scenes/Bootstrap.unity`** và Press Play. Console hiện log `[Bootstrap] Step N/M: ...`.
6. **Chạy EditMode tests** qua `Window → General → Test Runner`. Khoảng 55 cases phải green.
7. **Đọc `CLAUDE.md`** trước khi extend — index toàn bộ convention + footgun. Cặp với [docs/dev/PITFALLS.md](docs/dev/PITFALLS.md).

## Trạng thái phase

5 phase đã hoàn thành và merged vào `main`. Chi tiết: [docs/dev/JOURNAL.md](docs/dev/JOURNAL.md).

| Phase | Phạm vi |
|---|---|
| 1a / 1b | Foundation: asmdef restructure, event bus, localization wrap, save hardening, pool refactor, tests + CI |
| 2 | Real Input + Audio + Notification (wrap Unity packages) |
| 3 | UI scaffolding: popup stack, screens, transitions, toast, localized text, consumer-owned `UIRoot` |
| 4 | Gameplay scaffolding: state machine, level loader, lifecycle events |
| 5a | Live-Ops VersionCheck + DevTools (cheat console, FPS overlay) |
| 5b | Cross-cutting docs (file này) |

## License

MIT — xem [LICENSE](LICENSE).
