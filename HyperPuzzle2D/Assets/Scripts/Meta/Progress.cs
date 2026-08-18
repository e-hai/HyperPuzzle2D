using UnityEngine;

namespace HyperPuzzle2D.Meta
{
    /// <summary>
    /// Local persistence for stage unlocks, best scores/stars, and preference toggles.
    /// </summary>
    public static class Progress
    {
        const string SfxKey = "pref_sfx";
        const string HapticsKey = "pref_haptics";
        const string RunsKey = "runs_started";
        const string StageUnlockedKey = "stage_unlocked";
        const string LastPlayedStageKey = "last_played_stage";
        const string StageBestPrefix = "stage_best_";
        const string StageStarsPrefix = "stage_stars_";

        /// <summary>
        /// Namespaces the run/stage keys so separate game modes keep separate save files.
        /// Device preferences (sound, haptics) stay unprefixed on purpose.
        /// </summary>
        public static string Profile { get; set; } = string.Empty;

        static string Key(string key) => Profile + key;

        /// <summary>
        /// Highest 1-based stage number the player may enter. Starts at 1 (first stage unlocked).
        /// </summary>
        public static int StageUnlocked
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(Key(StageUnlockedKey), 1));
            private set
            {
                PlayerPrefs.SetInt(Key(StageUnlockedKey), value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>0-based index of the last stage the player started.</summary>
        public static int LastPlayedStage
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(Key(LastPlayedStageKey), 0));
            set
            {
                PlayerPrefs.SetInt(Key(LastPlayedStageKey), Mathf.Max(0, value));
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

        public static int RunsStarted => PlayerPrefs.GetInt(Key(RunsKey), 0);

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
            return PlayerPrefs.GetInt(Key(StageBestPrefix + zeroBasedIndex), 0);
        }

        public static int StageStars(int zeroBasedIndex)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(Key(StageStarsPrefix + zeroBasedIndex), 0), 0, 3);
        }

        /// <summary>
        /// Records a finished stage run. Best score and best stars are kept independently.
        /// Returns true when this run beat the stored score.
        /// </summary>
        public static bool SubmitStage(int zeroBasedIndex, int score, int stars)
        {
            var beatBest = score > StageBest(zeroBasedIndex);
            if (beatBest)
            {
                PlayerPrefs.SetInt(Key(StageBestPrefix + zeroBasedIndex), score);
            }

            if (stars > StageStars(zeroBasedIndex))
            {
                PlayerPrefs.SetInt(Key(StageStarsPrefix + zeroBasedIndex), Mathf.Clamp(stars, 0, 3));
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

        public static void MarkRunStarted()
        {
            PlayerPrefs.SetInt(Key(RunsKey), RunsStarted + 1);
            PlayerPrefs.Save();
        }
    }
}
