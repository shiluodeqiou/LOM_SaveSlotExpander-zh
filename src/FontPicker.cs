using System;
using UnityEngine;
using UnityEngine.UI;

namespace LOM.SaveSlotExpander
{
    /// <summary>注释使用哪种字体。</summary>
    public enum CommentFontMode
    {
        /// <summary>使用游戏自带字体；遇到游戏字体没有的字才自动换成别的字体。</summary>
        Auto,

        /// <summary>始终使用游戏自带字体（缺字会显示成方块）。</summary>
        GameFont,

        /// <summary>使用 CommentFontName 指定的字体。</summary>
        Custom,
    }

    /// <summary>
    /// 选择注释渲染用的字体。
    ///
    /// 本作是中文游戏，游戏界面正在用的字体就是正确的中文字体，
    /// 所以默认的做法是什么都不做，直接把界面字体借过来用。
    /// 只有在它画不出某个字的时候，才去别处找一个能画的。
    ///
    /// 这样做同时也照顾了字体替换类 MOD：如果玩家装了把界面字体换掉的
    /// 本地化 MOD，借到手的自然就是那个字体，不需要本 MOD 另外判断。
    ///
    /// 需要准备回退，是因为本作内嵌的字库是繁体字库（思源宋体 TC）。
    /// 简体字绝大多数都在里面，但个别生僻字可能缺。
    /// </summary>
    internal static class FontPicker
    {
        /// <summary>
        /// 判断字体够不够用时用的样本：常用汉字加上中英文标点。
        /// 输入框要显示玩家打进去的任何东西，没法拿具体内容去试，所以用这个样本。
        /// </summary>
        private const string ChineseProbe = "测试中文汉字，。！？、：；（）「」";

        /// <summary>
        /// 在已经加载进来的字体里找中文字体时的线索，按先后顺序匹配。
        /// 本作只会加载自己的字库，所以这里主要是给打过字体替换 MOD 的环境兜底。
        /// </summary>
        private static readonly string[] ChineseFontHints =
        {
            "SourceHanSerifSC", "Source Han Serif SC", "思源宋体",
            "NotoSerifSC", "Noto Serif SC",
            "SourceHanSansSC", "Source Han Sans SC", "思源黑体",
            "NotoSansSC", "Noto Sans SC",
            "SourceHanSerifTC", "Source Han Serif TC", "SourceHanSerifTW",
        };

        private static Font _fallback;      // 最近一次找到的可用回退字体
        private static Font _osFont;
        private static bool _osTried;

        private static Font _customFont;
        private static bool _customResolved;

        /// <summary>
        /// 如果 preferred 能把 text 全部画出来就直接用它，否则找一个能画出来的字体。
        /// 正常情况下拿到的就是游戏自带字体，只有遇到缺字才会换。
        /// </summary>
        public static Font For(string text, Font preferred)
        {
            if (CanRender(preferred, text)) return preferred;

            // 上次用过的回退字体先试，省掉每次都全量扫描
            if (CanRender(_fallback, text)) return _fallback;

            var named = FindByName(Plugin.CommentFontName.Value);
            if (CanRender(named, text)) return Remember(named);

            foreach (var hint in ChineseFontHints)
            {
                var found = FindByName(hint);
                if (CanRender(found, text)) return Remember(found);
            }

            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                if (CanRender(font, text)) return Remember(font);

            var os = OsFont();
            if (CanRender(os, text)) return Remember(os);

            Plugin.Log.LogWarning("找不到能显示这段文字的字体，缺的字会变成方块: " + text);
            return preferred ?? os;
        }

        /// <summary>注释用的字体。按 CommentFontMode 的设置决定。</summary>
        public static Font ForComment(string text, Font baseFont)
        {
            switch (Plugin.CommentFontMode.Value)
            {
                case CommentFontMode.GameFont:
                    return baseFont;

                case CommentFontMode.Custom:
                {
                    var custom = CustomFont(baseFont);
                    if (CanRender(custom, text)) return custom;
                    return For(text, custom ?? baseFont);
                }

                default:
                    return For(text, baseFont);
            }
        }

        /// <summary>
        /// 输入框用的字体。要能显示玩家可能打进去的字，所以先用样本试一遍，
        /// 样本都画不出来的字体不会用在输入框上。
        /// </summary>
        public static Font ForInput(Font preferred)
        {
            return For(ChineseProbe, preferred);
        }

        /// <summary>font 能不能把 text 里的字全部画出来。</summary>
        public static bool CanRender(Font font, string text)
        {
            if (font == null) return false;
            if (string.IsNullOrEmpty(text)) return true;

            try
            {
                foreach (char c in text)
                {
                    if (c == ' ' || c == '　' || c == '\t' || c == '\n' || c == '\r') continue;
                    if (!font.HasCharacter(c)) return false;
                }
                return true;
            }
            catch (Exception)
            {
                // 非动态字体上 HasCharacter 可能不可用
                return false;
            }
        }

        /// <summary>
        /// CommentFontName 指定的字体。没指定或没匹配上时退回游戏自带字体。
        /// </summary>
        private static Font CustomFont(Font baseFont)
        {
            if (_customResolved) return _customFont ?? baseFont;
            _customResolved = true;

            var named = FindByName(Plugin.CommentFontName.Value);
            if (named == null)
            {
                Plugin.Log.LogWarning("CommentFontName 没有匹配到任何已加载的字体，改用游戏自带字体: "
                                      + Plugin.CommentFontName.Value);
                return baseFont;
            }

            _customFont = named;
            Plugin.Log.LogInfo("注释字体（由设置指定）: " + named.name);
            return _customFont;
        }

        private static Font Remember(Font font)
        {
            _fallback = font;
            return font;
        }

        private static Font FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                if (font != null && font.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
            return null;
        }

        /// <summary>用系统里装的中文字体建一个动态字体，作为最后的回退。</summary>
        private static Font OsFont()
        {
            if (_osTried) return _osFont;
            _osTried = true;

            try
            {
                _osFont = Font.CreateDynamicFontFromOSFont(
                    new[]
                    {
                        "Noto Serif CJK SC", "Noto Serif SC", "Source Han Serif SC", "思源宋体",
                        "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "思源黑体",
                        "Microsoft YaHei", "微软雅黑",
                        "SimSun", "宋体", "NSimSun", "新宋体",
                        "DengXian", "等线",
                        "SimHei", "黑体",
                        "MingLiU", "細明體", "PMingLiU", "新細明體",
                        "Microsoft JhengHei", "微軟正黑體",
                    },
                    32);
                if (_osFont != null) Plugin.Log.LogInfo("回退字体（从系统字体新建）: " + _osFont.name);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("从系统字体新建字体失败: " + e.Message);
            }
            return _osFont;
        }
    }
}
