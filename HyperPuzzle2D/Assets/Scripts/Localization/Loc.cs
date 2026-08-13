using System;
using System.Collections.Generic;
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
    /// Tiny key→string table with en / zh and a PlayerPrefs-backed language preference.
    /// Changing language raises <see cref="Changed"/> so UI can rebind without a restart.
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "pref_language";

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["hud.score"] = "SCORE {0}",
            ["hud.ammo"] = "AMMO {0}",
            ["hud.scoreCaption"] = "SCORE",
            ["hud.ammoCaption"] = "AMMO",
            ["hud.targets"] = "{0}  ·  {1} LEFT",
            ["result.score"] = "SCORE  {0}",
            ["result.best"] = "BEST  {0}",
            ["result.newBest"] = "NEW BEST!",
            ["fail.title"] = "OUT OF AMMO",
            ["fail.revive"] = "WATCH AD TO REVIVE",
            ["fail.retry"] = "RETRY",
            ["clear.title"] = "CLEARED!",
            ["clear.next"] = "NEXT",
            ["common.menu"] = "MENU",
            ["menu.title"] = "HYPER SMASH",
            ["menu.tagline"] = "TOPPLE EVERY TARGET",
            ["menu.best"] = "BEST  {0}",
            ["menu.daily.best"] = "DAILY BEST  {0}",
            ["menu.play"] = "PLAY",
            ["menu.daily"] = "DAILY CHALLENGE",
            ["menu.sfx.on"] = "SFX  ON",
            ["menu.sfx.off"] = "SFX  OFF",
            ["menu.haptics.on"] = "VIBRATE  ON",
            ["menu.haptics.off"] = "VIBRATE  OFF",
            ["menu.lang"] = "中文",
            ["ftue.aim"] = "DRAG TO AIM  ·  RELEASE TO FIRE",
            ["level.TOWER"] = "TOWER",
            ["level.GATE"] = "GATE",
            ["level.TWINS"] = "TWINS",
            ["level.PYRAMID"] = "PYRAMID",
            ["level.TABLE"] = "TABLE",
            ["level.VAULT"] = "VAULT",
            ["level.SHELVES"] = "SHELVES",
            ["level.WALL"] = "WALL",
        };

        static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            ["hud.score"] = "得分 {0}",
            ["hud.ammo"] = "弹药 {0}",
            ["hud.scoreCaption"] = "得分",
            ["hud.ammoCaption"] = "弹药",
            ["hud.targets"] = "{0}  ·  剩余 {1}",
            ["result.score"] = "得分  {0}",
            ["result.best"] = "最高  {0}",
            ["result.newBest"] = "新纪录！",
            ["fail.title"] = "弹药用尽",
            ["fail.revive"] = "看广告复活",
            ["fail.retry"] = "再来一局",
            ["clear.title"] = "全部清空！",
            ["clear.next"] = "下一关",
            ["common.menu"] = "主菜单",
            ["menu.title"] = "HYPER SMASH",
            ["menu.tagline"] = "一炮打翻全部目标",
            ["menu.best"] = "最高分  {0}",
            ["menu.daily.best"] = "今日挑战最佳  {0}",
            ["menu.play"] = "开始游戏",
            ["menu.daily"] = "每日挑战",
            ["menu.sfx.on"] = "音效  开",
            ["menu.sfx.off"] = "音效  关",
            ["menu.haptics.on"] = "震动  开",
            ["menu.haptics.off"] = "震动  关",
            ["menu.lang"] = "EN",
            ["ftue.aim"] = "拖动瞄准  ·  松手发射",
            ["level.TOWER"] = "高塔",
            ["level.GATE"] = "拱门",
            ["level.TWINS"] = "双塔",
            ["level.PYRAMID"] = "金字塔",
            ["level.TABLE"] = "独腿桌",
            ["level.VAULT"] = "金库",
            ["level.SHELVES"] = "双层架",
            ["level.WALL"] = "城墙",
        };

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

        public static string Get(string key)
        {
            var table = Active == GameLanguage.Chinese ? Zh : En;
            if (table.TryGetValue(key, out var value))
            {
                return value;
            }

            return En.TryGetValue(key, out value) ? value : key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static string LevelName(string code)
        {
            var key = "level." + code;
            var localized = Get(key);
            return localized == key ? code : localized;
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
