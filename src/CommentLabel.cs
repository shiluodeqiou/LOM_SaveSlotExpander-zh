using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LOM.SaveSlotExpander
{
    /// <summary>记住挂在存档位行上的注释标签，方便下次直接取用。</summary>
    internal class CommentLabelSet : MonoBehaviour
    {
        public Text[] Labels;
        public Font[] BaseFonts;
    }

    /// <summary>
    /// 把注释用一个独立的 Text 显示出来。
    ///
    /// 如果直接往标题（第○年○月○旬）的 Text 上接文字，那个 Text 的 Best Fit 会生效，
    /// 连日期一起被缩小。所以这里复制现有元素做一条独立的标签，放在日期时间下面一行。
    /// 完全不碰标题，日期的字号就不会变。
    ///
    /// 行的外观有普通／选中等等状态之分，_timeText 里放着多个 Text 就是这个原因，
    /// 复制出来的标签数量也跟它保持一致。
    ///
    /// 行与行之间大约只空得下一行，所以把标签的高度固定成 CommentMaxLines 行，
    /// 让文字缩小到能放进去为止。这样注释再长也不会压到下面的存档位。
    /// </summary>
    internal static class CommentLabel
    {
        private const string ObjectName = "LOM_SSE_CommentLabel";

        public static void Apply(Component panel, Text[] titles, Text[] times, string slot, bool hasData)
        {
            if (!Plugin.EnableComments.Value || panel == null) return;

            string comment = hasData ? CommentStore.Get(slot) : "";

            var set = panel.GetComponent<CommentLabelSet>();
            if (set == null)
            {
                if (string.IsNullOrEmpty(comment)) return;   // 用不到就先不建
                set = Build(panel, times, titles);
                if (set == null) return;
            }

            bool show = !string.IsNullOrEmpty(comment);
            string shown = "";
            if (show)
            {
                try { shown = string.Format(Plugin.CommentLabelFormat.Value, comment); }
                catch (FormatException) { shown = "【" + comment + "】"; }
            }

            for (int i = 0; i < set.Labels.Length; i++)
            {
                var label = set.Labels[i];
                if (label == null) continue;

                if (!show)
                {
                    label.gameObject.SetActive(false);
                    continue;
                }

                label.text = shown;
                // 用游戏界面的字体，注释的字形就跟界面保持一致
                label.font = FontPicker.ForComment(shown, set.BaseFonts[i]);
                label.gameObject.SetActive(true);
            }
        }

        private static CommentLabelSet Build(Component panel, Text[] times, Text[] titles)
        {
            var sources = Usable(times);
            if (sources.Count == 0) sources = Usable(titles);
            if (sources.Count == 0) return null;

            // 换行宽度对齐标题（第○年○月○旬）那一栏。
            // 日期时间那一栏是铺满整行的，照搬的话会一直伸到屏幕外才换行，
            // 看上去就等于没有换行。
            float wrapWidth = 0f;
            var titleList = Usable(titles);
            if (titleList.Count > 0) wrapWidth = titleList[0].rectTransform.rect.width;

            var labels = new List<Text>();
            var baseFonts = new List<Font>();

            foreach (var source in sources)
            {
                var label = CreateLabel(source, wrapWidth);
                if (label == null) continue;
                labels.Add(label);
                baseFonts.Add(label.font);
            }

            if (labels.Count == 0) return null;

            var set = panel.gameObject.AddComponent<CommentLabelSet>();
            set.Labels = labels.ToArray();
            set.BaseFonts = baseFonts.ToArray();
            return set;
        }

        private static List<Text> Usable(Text[] array)
        {
            var list = new List<Text>();
            if (array == null) return list;
            foreach (var t in array)
                if (t != null && t.transform.parent != null) list.Add(t);
            return list;
        }

        private static Text CreateLabel(Text source, float wrapWidth)
        {
            Text label;
            try
            {
                var go = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
                go.name = ObjectName;
                label = go.GetComponent<Text>();
                if (label == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return null;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("创建注释标签失败: " + e.Message);
                return null;
            }

            label.supportRichText = false;
            if (Plugin.CommentFontSize.Value > 0) label.fontSize = Plugin.CommentFontSize.Value;

            // 缩到能放进指定的行数为止。
            // 跟标题不同，这是专用的标签，缩小了也不会影响日期的字号。
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Min(Plugin.CommentMinFontSize.Value, label.fontSize);

            // 父节点上就算有 LayoutGroup 也不会被挤走
            var element = label.GetComponent<LayoutElement>();
            if (element == null) element = label.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;

            // 挂着 ContentSizeFitter 的话，外框会跟着内容一起变宽，
            // 那么无论怎么设置换行都不会换
            var fitter = label.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            var src = source.rectTransform;
            var rt = label.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;

            // 把宽度定下来。锚点不管是 stretch 还是 point 都能生效，
            // 做法是把目标宽度与当前实宽的差值加到 sizeDelta 上。
            float desiredWidth = Plugin.CommentWidth.Value > 1f
                ? Plugin.CommentWidth.Value
                : (wrapWidth > 1f ? wrapWidth : src.rect.width);

            if (desiredWidth > 1f)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x + (desiredWidth - rt.rect.width), rt.sizeDelta.y);

            float line = src.rect.height;
            if (line < 1f) line = label.fontSize * 1.2f;

            // 高度按行数固定。Best Fit 会把文字缩到刚好放进这个范围。
            float grown = line * Plugin.CommentMaxLines.Value - rt.rect.height;
            if (grown > 0f) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.y + grown);
            else grown = 0f;

            // 放在复制来源的下面一行。
            // 把外框纵向撑大时，视 pivot 而定会往上也长出去、盖住日期时间，
            // 所以按 grown * (1 - pivot.y) 往下补回来，让上边缘不动。
            rt.anchoredPosition = src.anchoredPosition + new Vector2(
                Plugin.CommentOffsetX.Value,
                -line + Plugin.CommentOffsetY.Value - grown * (1f - rt.pivot.y));

            LogOnce(label, source, wrapWidth, line);
            return label;
        }

        private static bool _logged;

        /// <summary>
        /// 只把第一个标签的尺寸和结构写进日志。
        /// 换行宽度或位置不合适时，好从日志里拿到该填进设置里的实测值。
        /// </summary>
        private static void LogOnce(Text label, Text source, float wrapWidth, float line)
        {
            if (_logged) return;
            _logged = true;

            var names = new List<string>();
            foreach (var c in label.GetComponents<Component>()) names.Add(c.GetType().Name);

            var rt = label.rectTransform;
            Plugin.Log.LogInfo(string.Format(
                "[注释标签] 宽={0:0}（标题栏={1:0} / 日期栏={2:0}） 高={3:0}（一行={4:0} × {5}行） "
                + "字号={6}～{7} pivot=({8:0.##},{9:0.##}) 组成=[{10}]",
                rt.rect.width, wrapWidth, source.rectTransform.rect.width,
                rt.rect.height, line, Plugin.CommentMaxLines.Value,
                label.resizeTextMinSize, label.resizeTextMaxSize,
                rt.pivot.x, rt.pivot.y,
                string.Join(",", names.ToArray())));
        }
    }
}
