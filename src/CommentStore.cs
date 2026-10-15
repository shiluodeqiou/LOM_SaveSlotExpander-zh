using System;
using System.Collections.Generic;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 每个存档位注释的存放处。
    ///
    /// 存档本体（GameSave）是用 BinaryFormatter 序列化的，往里加字段会破坏
    /// 与已有存档的兼容。所以注释单独放在存档同一个文件夹里的另一个文件中。
    /// 完全不碰存档本体，卸载 MOD 后游戏照样能玩。
    ///
    /// 格式是 ini，可以直接用文本编辑器改。
    /// </summary>
    internal static class CommentStore
    {
        private const string FileName = "SaveSlotComments.ini";

        private const string Header =
            "# 活侠传 存档位注释 (LOM 存档位扩展 汉化版)\r\n" +
            "#\r\n" +
            "# 格式为「存档位编号 = 注释」，可以直接用文本编辑器修改。\r\n" +
            "# 请在游戏退出后编辑（游戏运行中改了会被覆盖）。\r\n" +
            "# 请保持 UTF-8 编码。";

        private static readonly Dictionary<string, string> _comments =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private static bool _loaded;

        public static string Get(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return "";
            EnsureLoaded();
            string value;
            return _comments.TryGetValue(slot, out value) ? (value ?? "") : "";
        }

        public static void Set(string slot, string text)
        {
            if (string.IsNullOrEmpty(slot)) return;
            EnsureLoaded();

            text = IniFile.Sanitize(text);
            int max = Plugin.CommentMaxLength.Value;
            if (text.Length > max) text = text.Substring(0, max);

            if (text.Length == 0) _comments.Remove(slot);
            else _comments[slot] = text;

            Save();
        }

        public static void Remove(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            EnsureLoaded();
            if (_comments.Remove(slot)) Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            var path = SavePaths.SideFile(FileName);
            if (path == null) return;   // 还不知道存档文件夹在哪，下次再试
            _loaded = true;

            try
            {
                foreach (var kv in IniFile.Read(path)) _comments[kv.Key] = kv.Value;
                Plugin.Log.LogInfo("已读取存档位注释: " + _comments.Count + " 条 (" + path + ")");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("读取存档位注释失败: " + e.Message);
            }
        }

        private static void Save()
        {
            try
            {
                var path = SavePaths.SideFile(FileName);
                if (path == null) return;
                SavePaths.WriteAtomic(path, IniFile.Format(Header, _comments), withBom: true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("保存存档位注释失败: " + e.Message);
            }
        }
    }
}
