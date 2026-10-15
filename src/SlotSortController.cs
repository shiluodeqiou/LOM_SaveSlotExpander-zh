using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 守候切换排列顺序的按键。
    ///
    /// 只在存档／读档界面打开着的时候响应。
    /// 界面关着的时候按下去也看不出顺序变了，
    /// 连着按两下反而又切回去了。
    /// </summary>
    internal class SlotSortController : MonoBehaviour
    {
        private Key _hotkey = Key.F3;

        private void Awake()
        {
            if (!Enum.TryParse(Plugin.SortToggleHotkey.Value, true, out _hotkey))
            {
                Plugin.Log.LogWarning("无法解析 SortToggleHotkey '" + Plugin.SortToggleHotkey.Value
                                      + "'，改用 F3。");
                _hotkey = Key.F3;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_hotkey].wasPressedThisFrame) return;

            if (CommentEditor.IsOpen) return;          // 正在输入注释时忽略
            if (!SlotSorter.AnyPanelOpen()) return;    // 列表没打开时什么都不做

            Plugin.SortMode.Value = Plugin.SortMode.Value == SlotSortMode.RecentFirst
                ? SlotSortMode.Default
                : SlotSortMode.RecentFirst;

            SlotSorter.ReapplyToOpenPanels();
            Plugin.Log.LogInfo("已切换排列顺序: " + Plugin.SortMode.Value);
        }
    }
}
