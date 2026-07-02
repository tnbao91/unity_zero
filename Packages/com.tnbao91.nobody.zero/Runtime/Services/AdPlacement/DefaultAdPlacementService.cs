using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.AdPlacement
{
    public sealed class DefaultAdPlacementService : IAdPlacementService
    {
        private readonly IAdsService _ads;
        private readonly ILogService _log;
        private readonly Dictionary<string, PlacementState> _placements = new();

        public DefaultAdPlacementService(IAdsService ads, ILogService log)
        {
            _ads = ads;
            _log = log;
        }

        public bool CanShow(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) return false;
            if (!_placements.TryGetValue(placementId, out var p)) return false;
            if (p.SessionShowCount >= p.SessionCap) return false;
            if (Time.realtimeSinceStartup - p.LastShownTime < p.CooldownSec) return false;
            return _ads.IsReady(p.Type);
        }

        public async UniTask<AdShowResult> TryShowAsync(string placementId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(placementId))
            {
                _log.Warn("[ADPLACE] Null or empty placement id");
                return new AdShowResult(AdType.Interstitial, AdResult.Failed, placementId, "invalid placement id");
            }
            if (!_placements.TryGetValue(placementId, out var p))
            {
                _log.Warn($"[ADPLACE] Unknown placement '{placementId}'");
                return new AdShowResult(AdType.Interstitial, AdResult.Failed, placementId, "unknown placement");
            }
            if (!CanShow(placementId))
            {
                _log.Info($"[ADPLACE] '{placementId}' on cooldown or capped");
                return new AdShowResult(p.Type, AdResult.NotReady, placementId, "cooldown/cap");
            }

            var result = await _ads.ShowAsync(p.Type, placementId, ct);
            if (result.Result == AdResult.Shown || result.Result == AdResult.Rewarded)
            {
                p.LastShownTime = Time.realtimeSinceStartup;
                p.SessionShowCount++;
                _placements[placementId] = p;
            }
            return result;
        }

        public void RegisterPlacement(string placementId, AdType type, TimeSpan cooldown, int sessionCap)
        {
            if (placementId == null) throw new ArgumentNullException(nameof(placementId));
            if (placementId.Length == 0) throw new ArgumentException("Placement id must be non-empty.", nameof(placementId));
            if (sessionCap < 1) throw new ArgumentOutOfRangeException(nameof(sessionCap), sessionCap, "Session cap must be >= 1.");
            if (cooldown < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown), cooldown, "Cooldown must be >= 0.");

            _placements[placementId] = new PlacementState
            {
                Type = type,
                CooldownSec = (float)cooldown.TotalSeconds,
                SessionCap = sessionCap,
                LastShownTime = float.NegativeInfinity,
                SessionShowCount = 0,
            };
            _log.Info($"[ADPLACE] Registered '{placementId}' type={type} cooldown={cooldown.TotalSeconds}s cap={sessionCap}");
        }

        public void NotifyShown(string placementId)
        {
            if (string.IsNullOrEmpty(placementId)) return;
            if (_placements.TryGetValue(placementId, out var p))
            {
                p.LastShownTime = Time.realtimeSinceStartup;
                p.SessionShowCount++;
                _placements[placementId] = p;
            }
        }

        private struct PlacementState
        {
            public AdType Type;
            public float CooldownSec;
            public int SessionCap;
            public float LastShownTime;
            public int SessionShowCount;
        }
    }
}
