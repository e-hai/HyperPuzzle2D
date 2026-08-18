using HyperPuzzle2D.Art;
using UnityEngine;

namespace HyperPuzzle2D.Paper
{
    /// <summary>
    /// One stage = one character base + one outer outfit. The base (simple underclothes) always
    /// stays; only the outfit is jigsaw-cut and torn away.
    /// </summary>
    public readonly struct PaperLevel
    {
        /// <summary>Resources folder under Art/Chars/, e.g. "Maiden".</summary>
        public readonly string Character;

        /// <summary>Outfit folder under that character, e.g. "Teal".</summary>
        public readonly string Outfit;

        public readonly string NameCn;
        public readonly string NameEn;
        public readonly int Shots;
        public readonly int Goal;
        public readonly int TwoStarScore;
        public readonly int ThreeStarScore;
        public readonly int Cols;
        public readonly int Rows;

        /// <summary>Used when baking a mask / underclothes split from a legacy full sheet.</summary>
        public readonly float HeadBottom;
        public readonly float Waist;
        public readonly float SkirtBottom;

        public string BasePath => "Art/Chars/" + Character + "/Base";
        public string OutfitPath => "Art/Chars/" + Character + "/Outfits/" + Outfit + "/Outfit";
        public string MaskPath => "Art/Chars/" + Character + "/Outfits/" + Outfit + "/Mask";
        public string PortraitPath => OutfitPath;

        public PaperLevel(
            string character, string outfit, string nameCn, string nameEn,
            int shots, int goal, int twoStar, int threeStar,
            int cols, int rows,
            float headBottom, float waist, float skirtBottom)
        {
            Character = character;
            Outfit = outfit;
            NameCn = nameCn;
            NameEn = nameEn;
            Shots = shots;
            Goal = goal;
            TwoStarScore = twoStar;
            ThreeStarScore = threeStar;
            Cols = cols;
            Rows = rows;
            HeadBottom = headBottom;
            Waist = waist;
            SkirtBottom = skirtBottom;
        }

        public int StarsFor(int score)
        {
            if (score < Goal)
            {
                return 0;
            }

            if (score >= ThreeStarScore)
            {
                return 3;
            }

            return score >= TwoStarScore ? 2 : 1;
        }

        public static int ScoreForPiece(int pixelCount, int totalClothesPixels, float centreU, float centreV)
        {
            if (totalClothesPixels <= 0 || pixelCount <= 0)
            {
                return 20;
            }

            var areaShare = pixelCount / (float)totalClothesPixels;
            var areaScore = Mathf.RoundToInt(40f + areaShare * 900f);
            var dx = centreU - 0.5f;
            var dy = centreV - 0.55f;
            var centreBonus = Mathf.RoundToInt((1f - Mathf.Clamp01(dx * dx * 4f + dy * dy * 3f)) * 80f);
            return Mathf.Clamp(areaScore + centreBonus, 20, 280);
        }

        public static Color TierForScore(int score)
        {
            if (score >= 160) return Palette.Accent;
            if (score >= 100) return Palette.ExplosionCore;
            if (score >= 60) return Palette.AccentCool;
            return Palette.Brittle;
        }
    }

    /// <summary>
    /// Maiden proves multi-outfit (Teal + Violet on one base). Other characters ship one outfit each.
    /// </summary>
    public static class PaperLevelLibrary
    {
        static readonly PaperLevel[] Levels =
        {
            new PaperLevel("Maiden", "Teal", "纸之少女·青绿", "MAIDEN · TEAL", 9, 400, 1000, 1500, 7, 11, 0.71f, 0.55f, 0.42f),
            new PaperLevel("Maiden", "Violet", "纸之少女·紫罗兰", "MAIDEN · VIOLET", 9, 420, 1020, 1520, 7, 11, 0.71f, 0.55f, 0.42f),
            new PaperLevel("Sailor", "Navy", "深蓝水手", "NAVY SAILOR", 9, 420, 1020, 1520, 7, 11, 0.71f, 0.57f, 0.44f),
            new PaperLevel("Scholar", "Crimson", "赤发学者", "RED SCHOLAR", 8, 430, 1000, 1480, 7, 11, 0.71f, 0.55f, 0.42f),
            new PaperLevel("Miko", "Vermilion", "朱红巫女", "SHRINE MIKO", 8, 440, 1020, 1500, 6, 12, 0.71f, 0.58f, 0.20f),
            new PaperLevel("Ranger", "Forest", "森之游侠", "FOREST RANGER", 8, 440, 1030, 1520, 7, 11, 0.71f, 0.55f, 0.40f),
            new PaperLevel("Witch", "Night", "紫夜魔女", "NIGHT WITCH", 8, 450, 1040, 1540, 7, 11, 0.71f, 0.55f, 0.40f),
            new PaperLevel("Sakura", "Bloom", "樱花舞者", "SAKURA DANCER", 7, 400, 940, 1360, 6, 12, 0.71f, 0.57f, 0.18f),
        };

        public static int Count => Levels.Length;

        public static PaperLevel Get(int index)
        {
            return Levels[Mathf.Clamp(index, 0, Levels.Length - 1)];
        }
    }
}
