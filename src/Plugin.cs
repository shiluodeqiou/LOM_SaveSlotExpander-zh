using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 扩大《活侠传》的存档位数量，并允许给每个存档位写注释的 BepInEx 插件。
    ///
    /// 本工程是原作者 ame-chan 的 LOM_SaveSlotExpander（面向日语环境）的汉化版，
    /// 界面文字、日志、设置说明全部改为简体中文，字体策略也改成面向中文玩家。
    ///
    /// 版本号按发布日期取，格式为 年.月.日（例如 2026.10.16）。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "lom.zh.saveslotexpander";
        public const string NAME = "LOM 存档位扩展（汉化版）";

        /// <summary>版本号＝发布日期。BepInEx 按语义化版本解析，这个格式合法。</summary>
        public const string VERSION = "2026.10.16";

        /// <summary>本汉化版所基于的原版版本，只出现在日志里。</summary>
        internal const string UPSTREAM = "1.2.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<int> SlotCount;
        internal static ConfigEntry<bool> EnableMetaCache;

        internal static ConfigEntry<bool> EnableComments;
        internal static ConfigEntry<string> CommentHotkey;
        internal static ConfigEntry<string> CommentLabelFormat;
        internal static ConfigEntry<int> CommentMaxLength;
        internal static ConfigEntry<bool> DeleteCommentWithSave;

        internal static ConfigEntry<int> CommentFontSize;
        internal static ConfigEntry<float> CommentOffsetX;
        internal static ConfigEntry<float> CommentOffsetY;
        internal static ConfigEntry<float> CommentWidth;
        internal static ConfigEntry<int> CommentMaxLines;
        internal static ConfigEntry<int> CommentMinFontSize;
        internal static ConfigEntry<float> CommentBottomPadding;
        internal static ConfigEntry<string> CommentFontName;
        internal static ConfigEntry<CommentFontMode> CommentFontMode;

        internal static ConfigEntry<SlotSortMode> SortMode;
        internal static ConfigEntry<string> SortToggleHotkey;

        private void Awake()
        {
            Log = Logger;

            SlotCount = Config.Bind(
                "1.General", "SlotCount", 100,
                new ConfigDescription(
                    "存档／读档界面显示多少个存档位。游戏原本是 20。存档文件名是 Save_001～Save_999，所以最大 999。"
                    + "（游戏本体的光标移动逻辑要求至少 2 个存档位，所以最小是 2）",
                    new AcceptableValueRange<int>(2, 999)));

            EnableMetaCache = Config.Bind(
                "1.General", "EnableMetaCache", true,
                "开启后，存档列表改用轻量的元数据缓存来显示，而不是读出存档本体。"
                + "存档位调多以后，列表打开速度会有明显改善，一般保持开启即可。");

            EnableComments = Config.Bind(
                "2.Comment", "EnableComments", true,
                "是否允许给每个存档位写注释。");

            CommentHotkey = Config.Bind(
                "2.Comment", "CommentHotkey", "F2",
                "打开注释编辑框的按键。填 UnityEngine.InputSystem.Key 里的名字（例如 F2、F4、Backquote、Insert）。");

            CommentLabelFormat = Config.Bind(
                "2.Comment", "CommentLabelFormat", "【{0}】",
                "注释的显示格式，{0} 会被替换成注释正文。");

            CommentMaxLength = Config.Bind(
                "2.Comment", "CommentMaxLength", 40,
                new ConfigDescription("注释最多多少个字。", new AcceptableValueRange<int>(1, 200)));

            DeleteCommentWithSave = Config.Bind(
                "2.Comment", "DeleteCommentWithSave", true,
                "删除存档时，连同这个存档位的注释一起删掉。");

            CommentFontSize = Config.Bind(
                "3.CommentAppearance", "CommentFontSize", 0,
                new ConfigDescription(
                    "注释的字号。填 0 表示跟它复制过来的来源（日期时间那一行）一样大。",
                    new AcceptableValueRange<int>(0, 120)));

            CommentOffsetX = Config.Bind(
                "3.CommentAppearance", "CommentOffsetX", 0f,
                "注释显示位置的横向微调（像素），往右为正。");

            CommentOffsetY = Config.Bind(
                "3.CommentAppearance", "CommentOffsetY", 0f,
                "注释显示位置的纵向微调（像素），往上为正。"
                + "默认位置是日期时间那一行的下面一行，这里填相对该位置的偏移。");

            CommentWidth = Config.Bind(
                "3.CommentAppearance", "CommentWidth", 0f,
                "注释的换行宽度（像素）。填 0 表示跟标题（第○年○月○旬）那一栏对齐。"
                + "自动对齐的换行位置不合适时，可以直接填实测宽度。"
                + "游戏启动时的日志里有实测值可供参考。");

            CommentMaxLines = Config.Bind(
                "3.CommentAppearance", "CommentMaxLines", 1,
                new ConfigDescription(
                    "注释占几行。存档位与存档位之间大约只空得下一行，"
                    + "填 2 以上会压到下面那个存档位上。过长的注释会自动缩小字号以放进这些行里。",
                    new AcceptableValueRange<int>(1, 5)));

            CommentMinFontSize = Config.Bind(
                "3.CommentAppearance", "CommentMinFontSize", 12,
                new ConfigDescription(
                    "注释缩小时的字号下限。不会比这个更小，放不下的部分会被截掉。",
                    new AcceptableValueRange<int>(6, 60)));

            CommentBottomPadding = Config.Bind(
                "3.CommentAppearance", "CommentBottomPadding", 0f,
                "列表末尾追加的留白（像素）。填 0 表示用行高的一半。"
                + "没有这段留白的话，最后一个存档位的注释会跑到列表外面看不见。");

            CommentFontMode = Config.Bind(
                "3.CommentAppearance", "CommentFontMode", LOM.SaveSlotExpander.CommentFontMode.Auto,
                "注释用哪种字体。"
                + "Auto 是使用游戏自带字体，只有游戏字体缺字时才自动换成别的字体；"
                + "GameFont 是始终使用游戏自带字体；"
                + "Custom 是使用 CommentFontName 指定的字体。");

            CommentFontName = Config.Bind(
                "3.CommentAppearance", "CommentFontName", "",
                "注释用的字体名（部分匹配即可）。留空则按 CommentFontMode 自动决定。"
                + "实际用到的字体名会写进游戏启动时的日志，想改字体时可以拿它当线索。");

            SortMode = Config.Bind(
                "4.Sort", "SortMode", SlotSortMode.Default,
                "存档列表的排列顺序。Default 是按存档位编号排（游戏本来的顺序），"
                + "RecentFirst 是按存档时间从新到旧排。"
                + "读档界面里排在最前的是最近保存的那一档，存档界面里排在最前的是用来新建存档的空位。"
                + "在游戏里按键切换后，会保存到这里。");

            SortToggleHotkey = Config.Bind(
                "4.Sort", "SortToggleHotkey", "F3",
                "切换排列顺序的按键。只在存档／读档界面打开时生效。"
                + "填 UnityEngine.InputSystem.Key 里的名字（例如 F3、F5、Tab）。");

            try
            {
                new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
            }
            catch (Exception e)
            {
                Log.LogError("应用 Harmony 补丁失败: " + e);
                return;
            }

            var go = new GameObject("LOM_SaveSlotExpander_Runtime");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<ModRuntime>();
            go.AddComponent<SlotSortController>();
            if (EnableComments.Value) go.AddComponent<CommentEditor>();

            Log.LogInfo(NAME + " " + VERSION + " 加载完成（基于原版 " + UPSTREAM + "）(存档位数=" + SlotCount.Value
                        + ", 元数据缓存=" + EnableMetaCache.Value
                        + ", 注释=" + EnableComments.Value + ")");
        }
    }
}
