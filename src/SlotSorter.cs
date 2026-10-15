using System;
using System.Reflection;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;

namespace LOM.SaveSlotExpander
{
    /// <summary>存档列表的排列顺序。</summary>
    public enum SlotSortMode
    {
        /// <summary>按存档位编号排（游戏本来的顺序）。</summary>
        Default,

        /// <summary>按存档时间从新到旧排。</summary>
        RecentFirst,
    }

    /// <summary>
    /// 调整存档／读档界面上存档位的排列顺序。
    ///
    /// 动的只是屏幕上显示的顺序，存档位编号与存档数据的对应关系一概不变。
    /// 编号 017 的存档，重排之后仍然是 017。
    ///
    /// 排列顺序由行在父节点里的先后决定，所以用 SetSiblingIndex 来互换。
    /// 同时把游戏侧持有的存档位数组也按同样的顺序重排，
    /// 再调一次游戏原本的 SetButtonNavigation。
    /// 这样光标的上下移动才和看到的顺序一致。
    ///
    /// 排序规则本身放在 SlotOrder 里，那边有测试覆盖。
    /// </summary>
    internal static class SlotSorter
    {
        private static readonly MethodInfo _saveNav =
            AccessTools.Method(typeof(SaveGamePanel), "SetButtonNavigation");

        private static readonly MethodInfo _loadNav =
            AccessTools.Method(typeof(LoadGamePanel), "SetButtonNavigation");

        // ------------------------------------------------------------------

        public static void Apply(SaveGamePanel panel)
        {
            if (panel == null) return;

            var slots = SlotFields.SaveGamePanelSlots(panel);
            if (slots == null || slots.Length < 2) return;

            var keys = new SlotKey[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) return;   // 预料之外，不碰它，直接放弃
                var data = SlotFields.SaveSlotData(s);
                keys[i] = new SlotKey
                {
                    Slot = SlotFields.SaveSlotName(s),
                    HasData = data != null,
                    TimeTick = data != null ? data.TimeTick : 0L,
                };
            }

            // 存档界面把新建存档用的空位提到最前
            var order = SlotOrder.Resolve(keys, RecentFirst, emptySlotFirst: true);

            var sorted = new SaveSlotPanel[slots.Length];
            for (int i = 0; i < order.Length; i++) sorted[i] = slots[order[i]];

            ApplyOrder(sorted, panel);
            SlotFields.SaveGamePanelSlots(panel) = sorted;
            Invoke(_saveNav, panel);
        }

        public static void Apply(LoadGamePanel panel)
        {
            if (panel == null) return;

            var slots = SlotFields.LoadGamePanelSlots(panel);
            if (slots == null || slots.Length < 2) return;

            var keys = new SlotKey[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s == null) return;
                var data = SlotFields.LoadSlotData(s);
                keys[i] = new SlotKey
                {
                    Slot = SlotFields.LoadSlotName(s),
                    HasData = data != null,
                    TimeTick = data != null ? data.TimeTick : 0L,
                };
            }

            // 读档界面把最近保存的那一档提到最前
            var order = SlotOrder.Resolve(keys, RecentFirst, emptySlotFirst: false);

            var sorted = new LoadSlotPanel[slots.Length];
            for (int i = 0; i < order.Length; i++) sorted[i] = slots[order[i]];

            ApplyOrder(sorted, panel);
            SlotFields.LoadGamePanelSlots(panel) = sorted;
            Invoke(_loadNav, panel);
        }

        // ------------------------------------------------------------------

        private static bool RecentFirst => Plugin.SortMode.Value == SlotSortMode.RecentFirst;

        private static void ApplyOrder(Component[] sorted, Component panel)
        {
            Transform container = null;

            for (int i = 0; i < sorted.Length; i++)
            {
                var row = sorted[i] != null ? sorted[i].transform : null;
                if (row == null) continue;
                row.SetSiblingIndex(i);
                if (container == null) container = row.parent;
            }

            // 末尾的留白必须始终留在最后
            ListBottomSpacer.KeepLast(container);
        }

        private static void Invoke(MethodInfo method, object instance)
        {
            if (method == null) return;
            try { method.Invoke(instance, null); }
            catch (Exception e) { Plugin.Log.LogWarning("重新设置光标移动失败: " + e.Message); }
        }

        // ------------------------------------------------------------------

        /// <summary>把当前的排列顺序重新套用到已经打开的列表上。</summary>
        public static void ReapplyToOpenPanels()
        {
            foreach (var p in UnityEngine.Object.FindObjectsOfType<SaveGamePanel>()) Apply(p);
            foreach (var p in UnityEngine.Object.FindObjectsOfType<LoadGamePanel>()) Apply(p);
        }

        /// <summary>列表是否正开着。</summary>
        public static bool AnyPanelOpen()
        {
            return UnityEngine.Object.FindObjectsOfType<SaveGamePanel>().Length > 0
                   || UnityEngine.Object.FindObjectsOfType<LoadGamePanel>().Length > 0;
        }
    }
}
