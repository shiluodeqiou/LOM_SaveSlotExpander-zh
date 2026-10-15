using System;
using System.Reflection;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 把对存档位私有字段的引用集中在这里。
    /// </summary>
    internal static class SlotFields
    {
        internal static readonly AccessTools.FieldRef<SaveGamePanel, int> SaveGamePanelCount =
            AccessTools.FieldRefAccess<SaveGamePanel, int>("_slotCount");

        internal static readonly AccessTools.FieldRef<LoadGamePanel, int> LoadGamePanelCount =
            AccessTools.FieldRefAccess<LoadGamePanel, int>("_slotCount");

        internal static readonly AccessTools.FieldRef<SaveGamePanel, SaveSlotPanel[]> SaveGamePanelSlots =
            AccessTools.FieldRefAccess<SaveGamePanel, SaveSlotPanel[]>("_saveSlots");

        internal static readonly AccessTools.FieldRef<LoadGamePanel, LoadSlotPanel[]> LoadGamePanelSlots =
            AccessTools.FieldRefAccess<LoadGamePanel, LoadSlotPanel[]>("_saveSlots");

        internal static readonly AccessTools.FieldRef<SaveSlotPanel, GameSave> SaveSlotData =
            AccessTools.FieldRefAccess<SaveSlotPanel, GameSave>("_currentData");

        internal static readonly AccessTools.FieldRef<LoadSlotPanel, GameSave> LoadSlotData =
            AccessTools.FieldRefAccess<LoadSlotPanel, GameSave>("_currentData");

        internal static readonly AccessTools.FieldRef<SaveGamePanel, Transform> SaveGamePanelContainer =
            AccessTools.FieldRefAccess<SaveGamePanel, Transform>("_slotContainer");

        internal static readonly AccessTools.FieldRef<LoadGamePanel, Transform> LoadGamePanelContainer =
            AccessTools.FieldRefAccess<LoadGamePanel, Transform>("_slotContainer");

        internal static readonly AccessTools.FieldRef<SaveSlotPanel, string> SaveSlotName =
            AccessTools.FieldRefAccess<SaveSlotPanel, string>("_slot");

        internal static readonly AccessTools.FieldRef<LoadSlotPanel, string> LoadSlotName =
            AccessTools.FieldRefAccess<LoadSlotPanel, string>("_slot");

        internal static readonly AccessTools.FieldRef<RecentSaveSlotPanel, string> RecentSlotName =
            AccessTools.FieldRefAccess<RecentSaveSlotPanel, string>("_slot");

        internal static readonly AccessTools.FieldRef<SaveSlotPanel, Text[]> SaveSlotTitles =
            AccessTools.FieldRefAccess<SaveSlotPanel, Text[]>("_titleText");

        internal static readonly AccessTools.FieldRef<LoadSlotPanel, Text[]> LoadSlotTitles =
            AccessTools.FieldRefAccess<LoadSlotPanel, Text[]>("_titleText");

        internal static readonly AccessTools.FieldRef<RecentSaveSlotPanel, Text[]> RecentSlotTitles =
            AccessTools.FieldRefAccess<RecentSaveSlotPanel, Text[]>("_titleText");

        internal static readonly AccessTools.FieldRef<SaveSlotPanel, Text[]> SaveSlotTimes =
            AccessTools.FieldRefAccess<SaveSlotPanel, Text[]>("_timeText");

        internal static readonly AccessTools.FieldRef<LoadSlotPanel, Text[]> LoadSlotTimes =
            AccessTools.FieldRefAccess<LoadSlotPanel, Text[]>("_timeText");

        internal static readonly AccessTools.FieldRef<RecentSaveSlotPanel, Text[]> RecentSlotTimes =
            AccessTools.FieldRefAccess<RecentSaveSlotPanel, Text[]>("_timeText");

        /// <summary>给存档位挂上鼠标悬停追踪（没挂过才挂）。</summary>
        internal static void EnsureHoverTracker(Button button, string slot)
        {
            if (!Plugin.EnableComments.Value || button == null) return;
            var tracker = button.gameObject.GetComponent<SlotHoverTracker>()
                          ?? button.gameObject.AddComponent<SlotHoverTracker>();
            tracker.Slot = slot;
        }
    }

    /// <summary>记住鼠标正停在哪个存档位上。</summary>
    internal class SlotHoverTracker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Slot;
        internal static string Hovered;

        public void OnPointerEnter(PointerEventData eventData) => Hovered = Slot;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Hovered == Slot) Hovered = null;
        }

        private void OnDisable()
        {
            if (Hovered == Slot) Hovered = null;
        }
    }

    // ------------------------------------------------------------------
    // 扩大存档位数量
    // ------------------------------------------------------------------

    /// <summary>
    /// 替换存档界面的存档位数量。只在 Awake 调用 CreateSlot 之前改掉这个值，
    /// 存档位本身的生成仍然交给游戏原本的逻辑。
    /// </summary>
    [HarmonyPatch(typeof(SaveGamePanel), "Awake")]
    internal static class SaveGamePanel_Awake
    {
        private static void Prefix(SaveGamePanel __instance)
        {
            SlotFields.SaveGamePanelCount(__instance) = Plugin.SlotCount.Value;
        }

        // 行高要等存档位生成完才知道，所以放在 Postfix 里加
        private static void Postfix(SaveGamePanel __instance)
        {
            ListBottomSpacer.Ensure(SlotFields.SaveGamePanelContainer(__instance));
            SlotSorter.Apply(__instance);
            SlotCountLog.SavePanel(SlotFields.SaveGamePanelSlots(__instance));
        }
    }

    /// <summary>列表重建之后重新套用排列顺序。</summary>
    [HarmonyPatch(typeof(SaveGamePanel), "UpdateSlot")]
    internal static class SaveGamePanel_UpdateSlot
    {
        private static void Postfix(SaveGamePanel __instance) => SlotSorter.Apply(__instance);
    }

    /// <summary>
    /// 重排会让光标位置对不上，所以界面刚打开时重新选中第一项。
    /// 游戏侧选中发生在 UpdateSlot 之前，那时选中的第一项和现在的不是同一个。
    /// </summary>
    [HarmonyPatch(typeof(SaveGamePanel), "OnPanelOpen")]
    internal static class SaveGamePanel_OnPanelOpen
    {
        private static void Postfix(SaveGamePanel __instance)
        {
            var slots = SlotFields.SaveGamePanelSlots(__instance);
            if (slots != null && slots.Length > 0 && slots[0] != null) slots[0].SelectButton();
        }
    }

    /// <summary>替换读档界面的存档位数量。</summary>
    [HarmonyPatch(typeof(LoadGamePanel), "Awake")]
    internal static class LoadGamePanel_Awake
    {
        private static void Prefix(LoadGamePanel __instance)
        {
            SlotFields.LoadGamePanelCount(__instance) = Plugin.SlotCount.Value;
        }

        private static void Postfix(LoadGamePanel __instance)
        {
            ListBottomSpacer.Ensure(SlotFields.LoadGamePanelContainer(__instance));
            SlotSorter.Apply(__instance);
            SlotCountLog.LoadPanel(SlotFields.LoadGamePanelSlots(__instance));
        }
    }

    /// <summary>
    /// 把实际生成出来的存档位数量写进日志。
    ///
    /// 设置里填的数字，和游戏真正排出来的格子数不一定一致
    /// （游戏更新改动了内部结构就会不一致），所以把后者也留下来，
    /// 出问题时一眼就能看出补丁到底有没有生效。每个界面只记一次。
    /// </summary>
    internal static class SlotCountLog
    {
        private static bool _saveReported;
        private static bool _loadReported;

        internal static void SavePanel(Component[] slots)
        {
            if (_saveReported) return;
            _saveReported = true;
            Report("存档界面", slots);
        }

        internal static void LoadPanel(Component[] slots)
        {
            if (_loadReported) return;
            _loadReported = true;
            Report("读档界面", slots);
        }

        private static void Report(string panel, Component[] slots)
        {
            int count = slots == null ? 0 : slots.Length;
            Plugin.Log.LogInfo(panel + "实际生成存档位 " + count + " 个");
        }
    }

    [HarmonyPatch(typeof(LoadGamePanel), "UpdateSlot")]
    internal static class LoadGamePanel_UpdateSlot
    {
        private static void Postfix(LoadGamePanel __instance) => SlotSorter.Apply(__instance);
    }

    // ------------------------------------------------------------------
    // 列表显示加速（元数据缓存）
    // ------------------------------------------------------------------

    /// <summary>
    /// 只在列表显示期间，把存档本体的还原换成轻量的替身对象。
    /// 真正的读取路径（LoadGameData 等）不算列表显示，所以不受影响。
    ///
    /// SaveSystem 上同时有 GetSaveData(string) 和私有的 GetSaveData&lt;T&gt;(string)，
    /// 光看参数类型分不出是哪一个，所以连返回类型一起看才选得准。
    /// </summary>
    [HarmonyPatch]
    internal static class SaveSystem_GetSaveData
    {
        private static MethodBase TargetMethod()
        {
            foreach (var m in AccessTools.GetDeclaredMethods(typeof(SaveSystem)))
            {
                if (m.Name != "GetSaveData") continue;
                if (m.IsGenericMethodDefinition) continue;
                if (m.ReturnType != typeof(GameSave)) continue;

                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(string)) return m;
            }
            throw new MissingMethodException("Mortal.Core.SaveSystem", "GetSaveData(string):GameSave");
        }

        private static bool Prefix(string slot, ref GameSave __result)
        {
            if (!SaveMetaCache.MetaModeActive) return true;
            __result = SaveMetaCache.GetStub(slot);
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.SaveGameData), new[] { typeof(string) })]
    internal static class SaveSystem_SaveGameData
    {
        private static void Postfix(string slot) => SaveMetaCache.Invalidate(slot);
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.NewGameData))]
    internal static class SaveSystem_NewGameData
    {
        private static void Postfix(SaveSystem __instance) => SaveMetaCache.Invalidate(__instance.CurrentSlot);
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.NewActGameData))]
    internal static class SaveSystem_NewActGameData
    {
        private static void Postfix(SaveSystem __instance) => SaveMetaCache.Invalidate(__instance.CurrentSlot);
    }

    [HarmonyPatch(typeof(SaveSystem), nameof(SaveSystem.DeleteSaveData))]
    internal static class SaveSystem_DeleteSaveData
    {
        private static void Postfix(string slot)
        {
            SaveMetaCache.Invalidate(slot);
            if (Plugin.DeleteCommentWithSave.Value) CommentStore.Remove(slot);
        }
    }

    // ------------------------------------------------------------------
    // 各个存档位的显示
    // ------------------------------------------------------------------

    [HarmonyPatch(typeof(SaveSlotPanel), nameof(SaveSlotPanel.Setup))]
    internal static class SaveSlotPanel_Setup
    {
        private static void Prefix() => SaveMetaCache.EnterMetaMode();

        private static void Postfix(SaveSlotPanel __instance)
        {
            string slot = SlotFields.SaveSlotName(__instance);
            CommentLabel.Apply(__instance,
                               SlotFields.SaveSlotTitles(__instance),
                               SlotFields.SaveSlotTimes(__instance),
                               slot, __instance.HasSaveData);
            SlotFields.EnsureHoverTracker(__instance.SlotButton, slot);
        }

        private static void Finalizer() => SaveMetaCache.ExitMetaMode();
    }

    [HarmonyPatch(typeof(LoadSlotPanel), nameof(LoadSlotPanel.Setup))]
    internal static class LoadSlotPanel_Setup
    {
        private static void Prefix() => SaveMetaCache.EnterMetaMode();

        private static void Postfix(LoadSlotPanel __instance)
        {
            string slot = SlotFields.LoadSlotName(__instance);
            CommentLabel.Apply(__instance,
                               SlotFields.LoadSlotTitles(__instance),
                               SlotFields.LoadSlotTimes(__instance),
                               slot, __instance.HasSaveData);
            SlotFields.EnsureHoverTracker(__instance.SlotButton, slot);
        }

        private static void Finalizer() => SaveMetaCache.ExitMetaMode();
    }

    [HarmonyPatch(typeof(RecentSaveSlotPanel), nameof(RecentSaveSlotPanel.Setup))]
    internal static class RecentSaveSlotPanel_Setup
    {
        private static void Prefix() => SaveMetaCache.EnterMetaMode();

        private static void Postfix(RecentSaveSlotPanel __instance)
        {
            string slot = SlotFields.RecentSlotName(__instance);
            CommentLabel.Apply(__instance,
                               SlotFields.RecentSlotTitles(__instance),
                               SlotFields.RecentSlotTimes(__instance),
                               slot, __instance.HasSaveData);
            SlotFields.EnsureHoverTracker(__instance.SlotButton, slot);
        }

        private static void Finalizer() => SaveMetaCache.ExitMetaMode();
    }

    // ------------------------------------------------------------------
    // 编辑注释时防止误操作
    // ------------------------------------------------------------------

    /// <summary>
    /// 输入注释期间，按 Delete 键不要弹出删除确认框。
    /// </summary>
    [HarmonyPatch(typeof(LoadGamePanel), nameof(LoadGamePanel.OpenDeletePanel))]
    internal static class LoadGamePanel_OpenDeletePanel
    {
        private static bool Prefix() => !CommentEditor.IsOpen;
    }
}
