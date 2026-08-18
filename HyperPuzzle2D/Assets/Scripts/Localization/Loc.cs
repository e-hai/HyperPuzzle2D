using System;
using UnityEngine;

namespace HyperPuzzle2D.Localization
{
    public enum GameLanguage
    {
        /// <summary>Follows the device language; Chinese family → zh, everything else → en.</summary>
        Auto = 0,
        English = 1,
        Chinese = 2,
    }

    /// <summary>
    /// Language preference with a PlayerPrefs-backed toggle. Paper UI mostly inlines bilingual
    /// strings via <see cref="NeedsCjk"/>; this class only owns the preference and the caption
    /// for the settings language button.
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "pref_language";

        static GameLanguage _preference = (GameLanguage)(-1);

        public static event Action Changed;

        public static GameLanguage Preference
        {
            get
            {
                if (_preference < 0)
                {
                    _preference = (GameLanguage)PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.Auto);
                }

                return _preference;
            }
            set
            {
                if (Preference == value)
                {
                    return;
                }

                _preference = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Resolved language after applying Auto.</summary>
        public static GameLanguage Active
        {
            get
            {
                if (Preference == GameLanguage.English) return GameLanguage.English;
                if (Preference == GameLanguage.Chinese) return GameLanguage.Chinese;
                return IsChineseSystem() ? GameLanguage.Chinese : GameLanguage.English;
            }
        }

        public static bool NeedsCjk => Active == GameLanguage.Chinese;

        /// <summary>Forces the opposite of the currently resolved language.</summary>
        public static void ToggleZhEn()
        {
            Preference = Active == GameLanguage.Chinese ? GameLanguage.English : GameLanguage.Chinese;
        }

        /// <summary>Caption for the language toggle: shows the language you switch *to*.</summary>
        public static string LanguageToggleCaption()
        {
            return Active == GameLanguage.Chinese ? "EN" : "中文";
        }

        static bool IsChineseSystem()
        {
            var lang = Application.systemLanguage;
            return lang == SystemLanguage.Chinese
                || lang == SystemLanguage.ChineseSimplified
                || lang == SystemLanguage.ChineseTraditional;
        }
    }
}
