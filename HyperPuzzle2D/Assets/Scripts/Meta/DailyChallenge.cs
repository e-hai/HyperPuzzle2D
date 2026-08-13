using System;
using UnityEngine;

namespace HyperPuzzle2D.Meta
{
    public sealed class DailyChallenge
    {
        public int TodaySeed => GetSeed(DateTime.UtcNow.Date);

        public int GetSeed(DateTime utcDate)
        {
            // Stable deterministic seed from UTC date.
            return utcDate.Year * 10000 + utcDate.Month * 100 + utcDate.Day;
        }

        public int LoadBestScore(int seed)
        {
            return PlayerPrefs.GetInt(Key(seed), 0);
        }

        public void SaveBestScore(int seed, int score)
        {
            var key = Key(seed);
            var best = PlayerPrefs.GetInt(key, 0);
            if (score > best)
            {
                PlayerPrefs.SetInt(key, score);
                PlayerPrefs.Save();
            }
        }

        static string Key(int seed) => "daily_best_" + seed;
    }
}
