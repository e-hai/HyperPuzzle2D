using System;
using UnityEngine;

namespace HyperPuzzle2D.Ads
{
    /// <summary>
    /// Local stand-in for LevelPlay. Logs and instantly succeeds callbacks.
    /// </summary>
    public sealed class MockAdService : IAdService
    {
        public void ShowBanner()
        {
            Debug.Log("[MockAds] Banner shown");
        }

        public void HideBanner()
        {
            Debug.Log("[MockAds] Banner hidden");
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            Debug.Log("[MockAds] Interstitial");
            onClosed?.Invoke();
        }

        public void ShowRewarded(Action<bool> onResult)
        {
            Debug.Log("[MockAds] Rewarded granted");
            onResult?.Invoke(true);
        }
    }
}
