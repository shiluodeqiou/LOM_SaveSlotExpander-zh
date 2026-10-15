using System;
using System.Collections.Generic;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 在游戏里直接编辑存档位注释的小浮层。
    ///
    /// 运行时用 uGUI 的 InputField 拼出来。之所以不用 IMGUI，是因为 uGUI
    /// 的中文输入（输入法）能正常工作，而且可以直接沿用游戏自己的字体。
    /// </summary>
    internal class CommentEditor : MonoBehaviour
    {
        internal static bool IsOpen { get; private set; }

        private static readonly AccessTools.FieldRef<SaveGamePanel, InputButton> _saveCloseButton =
            AccessTools.FieldRefAccess<SaveGamePanel, InputButton>("_closeInputButton");

        private static readonly AccessTools.FieldRef<LoadGamePanel, InputButton> _loadCloseButton =
            AccessTools.FieldRefAccess<LoadGamePanel, InputButton>("_closeButton");

        private GameObject _root;
        private InputField _input;
        private Text _label;
        private Font _font;

        private string _slot;
        private GameObject _previousSelection;
        private bool _closing;
        private bool _navigationWasEnabled = true;
        private readonly List<InputButton> _suppressedButtons = new List<InputButton>();

        private Key _hotkey = Key.F2;

        private void Awake()
        {
            if (!Enum.TryParse(Plugin.CommentHotkey.Value, true, out _hotkey))
            {
                Plugin.Log.LogWarning("无法解析 CommentHotkey '" + Plugin.CommentHotkey.Value + "'，改用 F2。");
                _hotkey = Key.F2;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool hotkeyPressed = keyboard[_hotkey].wasPressedThisFrame;
            if (!hotkeyPressed) return;

            if (IsOpen) Close(save: false);
            else TryOpen();
        }

        // --------------------------------------------------------------
        // 开关
        // --------------------------------------------------------------

        private void TryOpen()
        {
            Component owner;
            string slot = ResolveSlot(out owner);
            if (string.IsNullOrEmpty(slot) || owner == null) return;

            EnsureUI(owner);
            if (_input == null) return;

            _slot = slot;
            _closing = false;
            IsOpen = true;

            _label.text = "存档位 " + slot + " 的注释　（Enter=确定 / Esc=取消）";
            _input.text = CommentStore.Get(slot);

            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            SuppressGameInput(true);

            _root.SetActive(true);
            _input.Select();
            _input.ActivateInputField();
            _input.caretPosition = _input.text.Length;
        }

        private void Close(bool save)
        {
            if (!IsOpen || _closing) return;
            _closing = true;

            if (save) CommentStore.Set(_slot, _input.text);

            _input.DeactivateInputField();
            _root.SetActive(false);
            IsOpen = false;

            SuppressGameInput(false);

            if (_previousSelection != null && _previousSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_previousSelection);
            _previousSelection = null;

            if (save) RefreshOpenPanels();

            _closing = false;
        }

        private void OnEndEdit(string _)
        {
            if (!IsOpen || _closing) return;
            Close(save: !_input.wasCanceled);
        }

        /// <summary>
        /// 编辑期间屏蔽游戏侧的按键操作。特别是 Delete 键会弹出删除确认框，
        /// 必须确实屏蔽掉。
        /// </summary>
        private void SuppressGameInput(bool suppress)
        {
            if (EventSystem.current != null)
            {
                // 记住原本的状态，关闭时恢复回去
                if (suppress) _navigationWasEnabled = EventSystem.current.sendNavigationEvents;
                EventSystem.current.sendNavigationEvents = suppress ? false : _navigationWasEnabled;
            }

            if (suppress)
            {
                _suppressedButtons.Clear();
                foreach (var p in FindObjectsOfType<SaveGamePanel>())
                {
                    var b = _saveCloseButton(p);
                    if (b != null) _suppressedButtons.Add(b);
                }
                foreach (var p in FindObjectsOfType<LoadGamePanel>())
                {
                    var b = _loadCloseButton(p);
                    if (b != null) _suppressedButtons.Add(b);
                }
            }

            foreach (var b in _suppressedButtons)
            {
                if (b == null) continue;
                try { b.SetActive(!suppress); } catch { /* 界面已经关掉之类的情况，忽略 */ }
            }

            if (!suppress) _suppressedButtons.Clear();
        }

        private static void RefreshOpenPanels()
        {
            foreach (var p in FindObjectsOfType<SaveSlotPanel>()) p.Setup();
            foreach (var p in FindObjectsOfType<LoadSlotPanel>()) p.Setup();
            foreach (var p in FindObjectsOfType<RecentSaveSlotPanel>()) p.Setup();
        }

        // --------------------------------------------------------------
        // 判断目标是哪个存档位
        // --------------------------------------------------------------

        /// <summary>
        /// 优先用鼠标正停在的那个存档位，没有的话用当前选中的那个。
        /// </summary>
        private static string ResolveSlot(out Component owner)
        {
            owner = null;

            if (!string.IsNullOrEmpty(SlotHoverTracker.Hovered))
            {
                var hovered = FindPanelForSlot(SlotHoverTracker.Hovered);
                if (hovered != null)
                {
                    owner = hovered;
                    return SlotHoverTracker.Hovered;
                }
            }

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return null;

            var save = selected.GetComponentInParent<SaveSlotPanel>();
            if (save != null) { owner = save; return SlotFields.SaveSlotName(save); }

            var load = selected.GetComponentInParent<LoadSlotPanel>();
            if (load != null) { owner = load; return SlotFields.LoadSlotName(load); }

            var recent = selected.GetComponentInParent<RecentSaveSlotPanel>();
            if (recent != null) { owner = recent; return SlotFields.RecentSlotName(recent); }

            var tracker = selected.GetComponentInParent<SlotHoverTracker>();
            if (tracker != null && !string.IsNullOrEmpty(tracker.Slot))
            {
                var panel = FindPanelForSlot(tracker.Slot);
                if (panel != null) { owner = panel; return tracker.Slot; }
            }

            return null;
        }

        private static Component FindPanelForSlot(string slot)
        {
            foreach (var p in FindObjectsOfType<SaveSlotPanel>())
                if (SlotFields.SaveSlotName(p) == slot) return p;
            foreach (var p in FindObjectsOfType<LoadSlotPanel>())
                if (SlotFields.LoadSlotName(p) == slot) return p;
            foreach (var p in FindObjectsOfType<RecentSaveSlotPanel>())
                if (SlotFields.RecentSlotName(p) == slot) return p;
            return null;
        }

        // --------------------------------------------------------------
        // 搭建界面
        // --------------------------------------------------------------

        private void EnsureUI(Component slotPanel)
        {
            var font = HarvestFont(slotPanel);
            if (_root != null && _font == font) return;

            if (_root != null) Destroy(_root);
            _font = font;

            _root = new GameObject("LOM_CommentEditorCanvas");
            DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root.AddComponent<GraphicRaycaster>();

            // 背后压暗，让玩家一眼看出正在编辑
            var dim = NewImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.65f));
            Stretch(dim.rectTransform);

            var window = NewImage(_root.transform, "Window", new Color(0.09f, 0.08f, 0.07f, 0.98f));
            var wr = window.rectTransform;
            wr.anchorMin = wr.anchorMax = wr.pivot = new Vector2(0.5f, 0.5f);
            wr.sizeDelta = new Vector2(1000f, 260f);
            wr.anchoredPosition = Vector2.zero;

            _label = NewText(window.transform, "Label", 30, TextAnchor.MiddleLeft, new Color(0.92f, 0.86f, 0.72f));
            Anchor(_label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -70f), new Vector2(-40f, -20f));

            var field = NewImage(window.transform, "Field", new Color(1f, 1f, 1f, 0.10f));
            Anchor(field.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(40f, -35f), new Vector2(-40f, 35f));

            var text = NewText(field.transform, "Text", 34, TextAnchor.MiddleLeft, Color.white);
            text.supportRichText = false;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 6f), new Vector2(-16f, -6f));

            var placeholder = NewText(field.transform, "Placeholder", 34, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.35f));
            placeholder.text = "输入注释…";
            Anchor(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 6f), new Vector2(-16f, -6f));

            var hint = NewText(window.transform, "Hint", 24, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.55f));
            hint.text = "Enter：确定　　Esc：取消";
            Anchor(hint.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 18f), new Vector2(-40f, 58f));

            _input = field.gameObject.AddComponent<InputField>();
            _input.targetGraphic = field;
            _input.textComponent = text;
            _input.placeholder = placeholder;
            _input.lineType = InputField.LineType.SingleLine;
            _input.characterLimit = Plugin.CommentMaxLength.Value;
            _input.onEndEdit.AddListener(OnEndEdit);

            _root.SetActive(false);
        }

        /// <summary>
        /// 从游戏界面借一个 Font 过来。游戏当前用的就是正确的中文字体，
        /// 所以直接沿用即可；缺字的情况由 FontPicker 去处理。
        /// </summary>
        private static Font HarvestFont(Component slotPanel)
        {
            Font candidate = null;

            var text = slotPanel != null ? slotPanel.GetComponentInChildren<Text>(true) : null;
            if (text != null && text.font != null) candidate = text.font;

            if (candidate == null)
            {
                foreach (var t in FindObjectsOfType<Text>())
                    if (t.font != null) { candidate = t.font; break; }
            }

            if (candidate == null) candidate = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 输入框里会出现什么字说不准，挑一个能显示常用汉字的字体
            return FontPicker.ForInput(candidate);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text NewText(Transform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
