using System;
using System.Reflection;
using HarmonyLib;
using Mortal.Core;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 查存档数据放在哪里的工具。
    ///
    /// 文件名不自己拼，而是去问游戏本体 SaveSystem 的私有方法 GetSaveDataFile。
    /// 原因之一是 Steam 版的用户文件夹名就是 SteamID，更要紧的是不去猜命名规则。
    ///
    /// 猜错的后果很严重：会把实际存在的存档显示成「没有数据」。
    /// 在那个状态下点一下存档位，游戏会当成空位、走 NewGameData()，
    /// 把真实文件用新存档覆盖掉。也就是说，显示错误会直接导致丢档，
    /// 所以这里必须一切以游戏本体为准。
    /// </summary>
    internal static class SavePaths
    {
        private static MethodInfo _getSaveDataFile;
        private static bool _methodResolved;
        private static bool _fallbackWarned;

        private static string _directory;
        private static bool _directoryFailed;

        /// <summary>游戏本体的 GetSaveDataFile。找不到就是 null。</summary>
        private static MethodInfo Method()
        {
            if (_methodResolved) return _getSaveDataFile;
            _methodResolved = true;

            _getSaveDataFile = AccessTools.Method(typeof(SaveSystem), "GetSaveDataFile", new[] { typeof(string) });
            if (_getSaveDataFile == null)
                Plugin.Log.LogError("找不到 SaveSystem.GetSaveDataFile。游戏更新可能改动了内部结构。");

            return _getSaveDataFile;
        }

        /// <summary>Save_&lt;存档位&gt;.dat 的完整路径。取不到时返回 null。</summary>
        public static string SaveFile(string slot)
        {
            var sys = SaveSystem.Instance;
            var method = Method();

            if (sys != null && method != null)
            {
                try
                {
                    return (string)method.Invoke(sys, new object[] { slot });
                }
                catch (Exception e)
                {
                    if (!_fallbackWarned)
                    {
                        _fallbackWarned = true;
                        Plugin.Log.LogWarning("调用 GetSaveDataFile 失败，改用原来的命名规则推路径: " + e.Message);
                    }
                }
            }

            // 最后的手段。就算走到这里，SaveMetaCache 在断定「没有数据」之前
            // 也会再找游戏本体确认一次，不会把有存档的格子误判成空位。
            var dir = SaveDirectory();
            return dir == null ? null : System.IO.Path.Combine(dir, "Save_" + slot + ".dat");
        }

        /// <summary>存档数据所在的文件夹。取不到时返回 null。</summary>
        public static string SaveDirectory()
        {
            if (_directory != null || _directoryFailed) return _directory;

            try
            {
                var sys = SaveSystem.Instance;
                var method = Method();
                if (sys == null) return null;   // 还没初始化，下次再试
                if (method == null) { _directoryFailed = true; return null; }

                var probe = (string)method.Invoke(sys, new object[] { "universe" });
                _directory = System.IO.Path.GetDirectoryName(probe);
                Plugin.Log.LogInfo("存档文件夹: " + _directory);
                return _directory;
            }
            catch (Exception e)
            {
                _directoryFailed = true;
                Plugin.Log.LogError("无法确定存档文件夹: " + e);
                return null;
            }
        }

        /// <summary>本 MOD 使用的附带文件路径。</summary>
        public static string SideFile(string fileName)
        {
            var dir = SaveDirectory();
            return dir == null ? null : System.IO.Path.Combine(dir, fileName);
        }

        /// <summary>
        /// 先写 tmp 再替换过去，中途崩溃也不会弄坏原有文件。
        /// 玩家会手动打开的文件带上 BOM（免得记事本里显示成乱码）。
        /// </summary>
        public static void WriteAtomic(string path, string contents, bool withBom = false)
        {
            var tmp = path + ".tmp";
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(tmp, contents, new System.Text.UTF8Encoding(withBom));
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            System.IO.File.Move(tmp, path);
        }
    }
}
