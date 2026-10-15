using System;
using System.Collections.Generic;

namespace LOM.SaveSlotExpander
{
    /// <summary>判断排列顺序时需要的、一个存档位的信息。</summary>
    internal struct SlotKey
    {
        public string Slot;      // "001" 之类
        public bool HasData;
        public long TimeTick;    // 存档时间
    }

    /// <summary>
    /// 决定排列顺序的规则本身。
    ///
    /// 既不依赖游戏也不依赖 Unity，可以脱离游戏验证。
    /// 这条规则直接左右界面上看到的样子，所以不凭印象写，用测试钉死。
    /// </summary>
    internal static class SlotOrder
    {
        /// <summary>
        /// 返回重排结果相对原数组的下标顺序。
        ///
        /// recentFirst 为假时按存档位编号升序。
        /// 为真时按「已有存档的从新到旧 → 空位按编号升序」。
        /// emptySlotFirst 为真时再额外把一个空位提到最前面
        /// （存档界面要把新建存档用的格子放在最上）。
        /// </summary>
        public static int[] Resolve(SlotKey[] items, bool recentFirst, bool emptySlotFirst)
        {
            if (items == null || items.Length == 0) return new int[0];

            var index = new List<int>(items.Length);
            for (int i = 0; i < items.Length; i++) index.Add(i);

            if (!recentFirst)
            {
                index.Sort((a, b) => string.CompareOrdinal(items[a].Slot, items[b].Slot));
                return index.ToArray();
            }

            var saved = new List<int>();
            var empty = new List<int>();
            foreach (var i in index)
            {
                if (items[i].HasData) saved.Add(i);
                else empty.Add(i);
            }

            // 时间相同时按编号排，免得每次的排列都不一样
            saved.Sort((a, b) =>
            {
                int byTime = items[b].TimeTick.CompareTo(items[a].TimeTick);
                return byTime != 0 ? byTime : string.CompareOrdinal(items[a].Slot, items[b].Slot);
            });
            empty.Sort((a, b) => string.CompareOrdinal(items[a].Slot, items[b].Slot));

            var result = new List<int>(items.Length);
            if (emptySlotFirst && empty.Count > 0)
            {
                result.Add(empty[0]);
                empty.RemoveAt(0);
            }
            result.AddRange(saved);
            result.AddRange(empty);
            return result.ToArray();
        }
    }
}
