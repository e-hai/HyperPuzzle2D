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
            ["hud.scoreCaption"] = "SCORE / GOAL",
            ["hud.ammoCaption"] = "AMMO",
            ["hud.scoreGoal"] = "{0} / {1}",
            ["hud.targets"] = "{0}  ·  {1} LEFT",
            ["result.score"] = "SCORE  {0}",
            ["result.scoreGoal"] = "SCORE  {0} / {1}",
            ["result.best"] = "BEST  {0}",
            ["result.newBest"] = "NEW BEST!",
            ["fail.title"] = "OUT OF AMMO",
            ["fail.revive"] = "WATCH AD TO REVIVE",
            ["fail.retry"] = "RETRY",
            ["fail.shortGoal"] = "{0} SHORT OF GOAL",
            ["fail.shortStars"] = "{0} SHORT OF {1}",
            ["clear.title"] = "CLEARED!",
            ["clear.next"] = "NEXT STAGE",
            ["clear.home"] = "HOME",
            ["clear.all"] = "ALL STAGES CLEARED!",
            ["clear.replay"] = "REPLAY",
            ["clear.replayStars"] = "REPLAY FOR ★★★",
            ["clear.moreFor"] = "{0} MORE FOR {1}",
            ["clear.perfect"] = "3 STARS!",
            ["pause.title"] = "PAUSED",
            ["pause.resume"] = "RESUME",
            ["pause.retry"] = "RESTART",
            ["hud.pause"] = "PAUSE",
            ["star.two"] = "★★",
            ["star.three"] = "★★★",
            ["common.menu"] = "HOME",
            ["common.back"] = "BACK",
            ["splash.tap"] = "TAP TO CONTINUE",
            ["menu.title"] = "HYPER SMASH",
            ["menu.tagline"] = "TOPPLE EVERY TARGET",
            ["menu.best"] = "BEST  {0}",
            ["menu.daily.best"] = "DAILY BEST  {0}",
            ["menu.stage.progress"] = "STAGES  {0} / {1}",
            ["menu.stars"] = "{0} / {1}",
            ["menu.stages"] = "STAGES",
            ["menu.endless"] = "ENDLESS",
            ["menu.daily"] = "DAILY CHALLENGE",
            ["menu.sfx.on"] = "SFX  ON",
            ["menu.sfx.off"] = "SFX  OFF",
            ["menu.haptics.on"] = "VIBRATE  ON",
            ["menu.haptics.off"] = "VIBRATE  OFF",
            ["menu.lang"] = "中文",
            ["menu.settings"] = "SETTINGS",
            ["settings.title"] = "SETTINGS",
            ["settings.close"] = "DONE",
            ["stage.title"] = "SELECT STAGE",
            ["stage.locked"] = "LOCKED",
            ["stage.item"] = "{0}.  {1}",
            ["hud.stage.targets"] = "STAGE {0}  ·  {1}  ·  {2} LEFT",
            ["ftue.aim"] = "DRAG TO AIM  ·  RELEASE TO FIRE",
            ["shot.tapCluster"] = "TAP TO SPLIT",
            ["shot.tapCharge"] = "TAP TO DETONATE",
            ["hud.clearCaption"] = "CLEAR ALL",
            ["hud.clearGoal"] = "{0} LEFT",
            ["fail.leftStanding"] = "{0} LEFT STANDING",
            ["stage.start"] = "START",
            ["stage.goal.score"] = "GOAL  {0}",
            ["stage.goal.clear"] = "CLEAR EVERY PIECE  ·  3★",
            ["stage.loadout"] = "LOADOUT  ·  TAP TWO TO SWAP",
            ["stage.swapNext"] = "SWAP NEXT",
            ["hint.TOWER"] = "Brittle face — one solid hit drops the stack",
            ["hint.GATE"] = "Hit the exposed red core under the roof",
            ["hint.TWINS"] = "Cluster both cores in one split",
            ["hint.PYRAMID"] = "Sweep the brittle base end to end",
            ["hint.TABLE"] = "One-shot the explosive leg",
            ["hint.VAULT"] = "Plant a charge on the brittle shell",
            ["hint.SHELVES"] = "Take either explosive foot",
            ["hint.WALL"] = "Center shot for the twin cores",
            ["feedback.smash"] = "SMASH!",
            ["feedback.smashx"] = "SMASH x{0}",
            ["feedback.chain"] = "CHAIN x{0}",
            ["feedback.collapse"] = "TOTAL COLLAPSE",
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
            ["hud.scoreCaption"] = "得分 / 目标",
            ["hud.ammoCaption"] = "弹药",
            ["hud.scoreGoal"] = "{0} / {1}",
            ["hud.targets"] = "{0}  ·  剩余 {1}",
            ["result.score"] = "得分  {0}",
            ["result.scoreGoal"] = "得分  {0} / {1}",
            ["result.best"] = "最高  {0}",
            ["result.newBest"] = "新纪录！",
            ["fail.title"] = "弹药用尽",
            ["fail.revive"] = "看广告复活",
            ["fail.retry"] = "再来一局",
            ["fail.shortGoal"] = "距目标还差 {0}",
            ["fail.shortStars"] = "距 {1} 还差 {0}",
            ["clear.title"] = "通关！",
            ["clear.next"] = "下一关",
            ["clear.home"] = "回主页",
            ["clear.all"] = "全部通关！",
            ["clear.replay"] = "再玩一次",
            ["clear.replayStars"] = "再玩冲三星",
            ["clear.moreFor"] = "再加 {0} 可得 {1}",
            ["clear.perfect"] = "三星通关！",
            ["pause.title"] = "已暂停",
            ["pause.resume"] = "继续",
            ["pause.retry"] = "重开本关",
            ["hud.pause"] = "暂停",
            ["star.two"] = "★★",
            ["star.three"] = "★★★",
            ["common.menu"] = "主页",
            ["common.back"] = "返回",
            ["splash.tap"] = "点击继续",
            ["menu.title"] = "HYPER SMASH",
            ["menu.tagline"] = "一炮打翻全部目标",
            ["menu.best"] = "最高分  {0}",
            ["menu.daily.best"] = "今日挑战最佳  {0}",
            ["menu.stage.progress"] = "闯关进度  {0} / {1}",
            ["menu.stars"] = "{0} / {1}",
            ["menu.stages"] = "闯关",
            ["menu.endless"] = "无尽模式",
            ["menu.daily"] = "每日挑战",
            ["menu.sfx.on"] = "音效  开",
            ["menu.sfx.off"] = "音效  关",
            ["menu.haptics.on"] = "震动  开",
            ["menu.haptics.off"] = "震动  关",
            ["menu.lang"] = "EN",
            ["menu.settings"] = "设置",
            ["settings.title"] = "设置",
            ["settings.close"] = "完成",
            ["stage.title"] = "选择关卡",
            ["stage.locked"] = "未解锁",
            ["stage.item"] = "{0}.  {1}",
            ["hud.stage.targets"] = "第{0}关  ·  {1}  ·  剩余 {2}",
            ["ftue.aim"] = "拖动瞄准  ·  松手发射",
            ["shot.tapCluster"] = "点击分裂",
            ["shot.tapCharge"] = "点击引爆",
            ["hud.clearCaption"] = "清场",
            ["hud.clearGoal"] = "剩余 {0}",
            ["fail.leftStanding"] = "还剩 {0} 块",
            ["stage.start"] = "开始",
            ["stage.goal.score"] = "目标分  {0}",
            ["stage.goal.clear"] = "必须清场  ·  三星",
            ["stage.loadout"] = "弹药队列  ·  点两发交换",
            ["stage.swapNext"] = "交换下两发",
            ["hint.TOWER"] = "脆块正面——一发扎实的就能推倒",
            ["hint.GATE"] = "打屋顶下外露的红色核心",
            ["hint.TWINS"] = "用集束弹同时打中两个核心",
            ["hint.PYRAMID"] = "横扫整条脆块基座",
            ["hint.TABLE"] = "一炮炸掉承重的爆炸腿",
            ["hint.VAULT"] = "把爆破包种在脆壳上",
            ["hint.SHELVES"] = "炸掉任意一只爆炸脚",
            ["hint.WALL"] = "对准墙心，一次带走双核",
            ["feedback.smash"] = "粉碎！",
            ["feedback.smashx"] = "粉碎 x{0}",
            ["feedback.chain"] = "连锁 x{0}",
            ["feedback.collapse"] = "全面崩塌",
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
