using UnityEngine;

namespace HyperPuzzle2D.Meta
{
    /// <summary>
    /// Local persistence for endless-mode progress, stage unlocks, and preference toggles.
    /// Daily best scores live in <see cref="DailyChallenge"/>.
    /// </summary>
    public static class Progress
    {
        const string EndlessBestKey = "endless_best";
        const string FtueDoneKey = "ftue_done";
        const string SfxKey = "pref_sfx";
        const string HapticsKey = "pref_haptics";
        const string RunsKey = "runs_started";
        const string StageUnlockedKey = "stage_unlocked";
        const string LastPlayedStageKey = "last_played_stage";
        const string StageBestPrefix = "stage_best_";
        const string StageStarsPrefix = "stage_stars_";

        /// <summary>Interstitial ads stay quiet for the first few runs so FTUE is not interrupted.</summary>
        public const int AdFreeRuns = 3;

        public static int EndlessBest => PlayerPrefs.GetInt(EndlessBestKey, 0);

        /// <summary>
        /// Highest 1-based stage number the player may enter. Starts at 1 (first stage unlocked).
        /// </summary>
        public static int StageUnlocked
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(StageUnlockedKey, 1));
            private set
            {
                PlayerPrefs.SetInt(StageUnlockedKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>0-based index of the last stage the player started.</summary>
        public static int LastPlayedStage
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(LastPlayedStageKey, 0));
            set
            {
                PlayerPrefs.SetInt(LastPlayedStageKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

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

        public static bool IsStageUnlocked(int zeroBasedIndex)
        {
            return zeroBasedIndex >= 0 && (zeroBasedIndex + 1) <= StageUnlocked;
        }

        /// <summary>
        /// After clearing stage <paramref name="zeroBasedIndex"/>, unlocks the next one.
        /// Returns true when a new stage became available.
        /// </summary>
        public static bool UnlockAfterClear(int zeroBasedIndex, int stageCount)
        {
            if (stageCount <= 0)
            {
                return false;
            }

            var next = Mathf.Clamp(zeroBasedIndex + 2, 1, stageCount);
            if (next <= StageUnlocked)
            {
                return false;
            }

            StageUnlocked = next;
            return true;
        }

        public static int StageBest(int zeroBasedIndex)
        {
            return PlayerPrefs.GetInt(StageBestPrefix + zeroBasedIndex, 0);
        }

        public static int StageStars(int zeroBasedIndex)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(StageStarsPrefix + zeroBasedIndex, 0), 0, 3);
        }

        /// <summary>
        /// Records a finished stage run. Best score and best stars are kept independently: a run
        /// that scores lower can still not take away a rating the player already earned.
        /// Returns true when this run beat the stored score.
        /// </summary>
        public static bool SubmitStage(int zeroBasedIndex, int score, int stars)
        {
            var beatBest = score > StageBest(zeroBasedIndex);
            if (beatBest)
            {
                PlayerPrefs.SetInt(StageBestPrefix + zeroBasedIndex, score);
            }

            if (stars > StageStars(zeroBasedIndex))
            {
                PlayerPrefs.SetInt(StageStarsPrefix + zeroBasedIndex, Mathf.Clamp(stars, 0, 3));
            }

            PlayerPrefs.Save();
            return beatBest;
        }

        public static int TotalStars(int stageCount)
        {
            var total = 0;
            for (var i = 0; i < stageCount; i++)
            {
                total += StageStars(i);
            }

            return total;
        }

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
