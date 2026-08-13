using UnityEngine;

namespace HyperPuzzle2D.Meta
{
    /// <summary>
    /// Local persistence for endless-mode progress and player preference toggles.
    /// Daily best scores live in <see cref="DailyChallenge"/>; this owns the all-time endless
    /// best, FTUE completion, and the SFX / haptics switches.
    /// </summary>
    public static class Progress
    {
        const string EndlessBestKey = "endless_best";
        const string FtueDoneKey = "ftue_done";
        const string SfxKey = "pref_sfx";
        const string HapticsKey = "pref_haptics";
        const string RunsKey = "runs_started";

        /// <summary>Interstitial ads stay quiet for the first few runs so FTUE is not interrupted.</summary>
        public const int AdFreeRuns = 3;

        public static int EndlessBest => PlayerPrefs.GetInt(EndlessBestKey, 0);

        public static bool FtueDone
        {
            get => PlayerPrefs.GetInt(FtueDoneKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(FtueDoneKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(HapticsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static int RunsStarted => PlayerPrefs.GetInt(RunsKey, 0);

        public static bool ShouldDelayInterstitial => RunsStarted <= AdFreeRuns;

        /// <summary>Stores the score if it beats the record. Returns true when a new best was set.</summary>
        public static bool SubmitEndless(int score)
        {
            if (score <= EndlessBest)
            {
                return false;
            }

            PlayerPrefs.SetInt(EndlessBestKey, score);
            PlayerPrefs.Save();
            return true;
        }

        public static void MarkRunStarted()
        {
            PlayerPrefs.SetInt(RunsKey, RunsStarted + 1);
            PlayerPrefs.Save();
        }
    }
}
