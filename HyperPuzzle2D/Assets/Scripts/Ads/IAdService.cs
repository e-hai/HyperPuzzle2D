using System;

namespace HyperPuzzle2D.Ads
{
    public interface IAdService
    {
        void ShowBanner();
        void HideBanner();
        void ShowInterstitial(Action onClosed = null);
        void ShowRewarded(Action<bool> onResult);
    }
}
