using System;
using UnityEngine;
using UnityEngine.UI;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 在存档列表末尾补一段留白。
    ///
    /// 注释画在每一行日期时间的下方，而最后一个存档位下面什么都没有，
    /// 注释就跑到列表内容的下边缘之外，被视口的遮罩裁掉看不见了。
    /// 在末尾加一个空元素把内容撑高，就能滚到那里。
    /// </summary>
    internal static class ListBottomSpacer
    {
        private const string ObjectName = "LOM_SSE_BottomSpacer";

        /// <summary>重排之后也让留白留在最后。</summary>
        public static void KeepLast(Transform container)
        {
            if (container == null) return;
            var spacer = container.Find(ObjectName);
            if (spacer != null) spacer.SetAsLastSibling();
        }

        public static void Ensure(Transform container)
        {
            if (!Plugin.EnableComments.Value || container == null) return;
            if (container.Find(ObjectName) != null) return;

            float height = Plugin.CommentBottomPadding.Value;
            if (height <= 0f) height = AutoHeight(container);
            if (height <= 0f) return;

            try
            {
                var go = new GameObject(ObjectName, typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(container, false);
                go.transform.SetAsLastSibling();

                var element = go.GetComponent<LayoutElement>();
                element.minHeight = height;
                element.preferredHeight = height;
                element.flexibleHeight = 0f;

                Plugin.Log.LogInfo("已在列表末尾追加留白: " + height.ToString("0") + "px");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("追加列表末尾留白失败: " + e.Message);
            }
        }

        /// <summary>以行高的一半为准，一行注释正好放得下。</summary>
        private static float AutoHeight(Transform container)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var rt = container.GetChild(i) as RectTransform;
                if (rt == null) continue;

                float h = rt.rect.height;
                if (h > 1f) return h * 0.5f;
            }
            return 0f;
        }
    }
}
