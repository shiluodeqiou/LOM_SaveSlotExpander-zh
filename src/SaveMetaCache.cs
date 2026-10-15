using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mortal.Core;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 只保留列表显示所需信息的缓存。
    ///
    /// 游戏原本的列表显示，会为每个存档位用 BinaryFormatter 把 60KB 以上的
    /// 存档本体整个还原出来。20 个存档位还行，扩到几百个以后每到打开界面
    /// 都会卡好几秒。所以这里只把显示要用的几项抽出来缓存到磁盘上。
    /// 缓存会拿文件的修改时刻和大小做校验，在游戏外替换过存档也会自动重读。
    /// </summary>
    internal static class SaveMetaCache
    {
        private const string FileName = "SaveSlotExpander_meta.txt";
        private const string FormatHeader = "# LOM 存档位扩展 元数据缓存 v1（删掉会自动重建）";

        private static readonly Dictionary<string, SlotMeta> _cache =
            new Dictionary<string, SlotMeta>(StringComparer.Ordinal);

        private static bool _loaded;
        private static bool _dirty;
        private static bool _reentrant;

        /// <summary>只有列表显示期间大于 0。这段时间里 GetSaveData 会返回替身对象。</summary>
        internal static int MetaModeDepth;

        internal static bool MetaModeActive => Plugin.EnableMetaCache.Value && MetaModeDepth > 0 && !_reentrant;

        internal static bool IsDirty => _dirty;

        internal static void EnterMetaMode() => MetaModeDepth++;

        internal static void ExitMetaMode()
        {
            if (MetaModeDepth > 0) MetaModeDepth--;
        }

        /// <summary>
        /// 返回列表显示用的轻量 GameSave。存档不存在时返回 null。
        /// 只填了显示和分支判断会用到的字段；真正的读取由游戏原本的路径
        /// 重新从磁盘上读，所以不受影响。
        /// </summary>
        public static GameSave GetStub(string slot)
        {
            var meta = GetMeta(slot);
            if (meta == null) return null;

            return new GameSave
            {
                TimeTick = meta.TimeTick,
                IsDemo = meta.IsDemo,
                NewGamePlus = meta.NewGamePlus,
                CurrentYear = meta.CurrentYear,
                CurrentMonth = meta.CurrentMonth,
                CurrentStage = (MonthStageType)meta.CurrentStage,
            };
        }

        private static SlotMeta GetMeta(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            EnsureLoaded();

            string file = SavePaths.SaveFile(slot);
            if (file == null || !File.Exists(file))
            {
                // 「没有数据」判断错了很危险，所以先找游戏本体确认再下结论。
                // 把实际存在的存档误显示成空位的话，一点那个存档位，
                // 游戏就会走 NewGameData()，把真实文件覆盖掉。
                // 空存档位的话，本体那边也只是 File.Exists 一次就返回 null，代价极小。
                var actual = ReadRealSave(slot);
                if (actual == null)
                {
                    if (_cache.Remove(slot)) _dirty = true;
                    return null;
                }

                // 本体说有数据 —— 说明本 MOD 看的路径和实际对不上。
                // 拿不到可用于校验的修改时刻和大小，所以不缓存，
                // 每次都让本体去读（慢是慢，总比丢档强）。
                WarnPathMismatch(slot, file);
                return FromSave(slot, actual, 0L, 0L);
            }

            var info = new FileInfo(file);
            long ticks = info.LastWriteTimeUtc.Ticks;
            long size = info.Length;

            SlotMeta cached;
            if (_cache.TryGetValue(slot, out cached) && cached.FileTicks == ticks && cached.FileSize == size)
                return cached;

            var save = ReadRealSave(slot);
            if (save == null)
            {
                if (_cache.Remove(slot)) _dirty = true;
                return null;
            }

            var meta = FromSave(slot, save, ticks, size);
            _cache[slot] = meta;
            _dirty = true;
            return meta;
        }

        private static SlotMeta FromSave(string slot, GameSave save, long ticks, long size)
        {
            return new SlotMeta
            {
                Slot = slot,
                FileTicks = ticks,
                FileSize = size,
                TimeTick = save.TimeTick,
                IsDemo = save.IsDemo,
                NewGamePlus = save.NewGamePlus,
                CurrentYear = save.CurrentYear,
                CurrentMonth = save.CurrentMonth,
                CurrentStage = (int)save.CurrentStage,
            };
        }

        private static bool _mismatchWarned;

        private static void WarnPathMismatch(string slot, string expected)
        {
            if (_mismatchWarned) return;
            _mismatchWarned = true;

            Plugin.Log.LogWarning(
                "存档文件的位置和预想的不一样（存档位 " + slot + " / 预想路径 " + expected + "）。"
                + "游戏更新可能改变了文件的存放方式。"
                + "显示结果仍然是对的，只是打开列表会变慢。");
        }

        /// <summary>临时关掉本 MOD 的钩子，去读真实数据。</summary>
        private static GameSave ReadRealSave(string slot)
        {
            _reentrant = true;
            try
            {
                return SaveSystem.Instance.GetSaveData(slot);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("读取存档位 " + slot + " 的元数据失败: " + e.Message);
                return null;
            }
            finally
            {
                _reentrant = false;
            }
        }

        public static void Invalidate(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            if (_cache.Remove(slot)) _dirty = true;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;

            var path = SavePaths.SideFile(FileName);
            if (path == null) return;   // 还不知道存档文件夹在哪，下次再试
            _loaded = true;

            try
            {
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    var meta = SlotMeta.Decode(line);
                    if (meta != null) _cache[meta.Slot] = meta;
                }
                Plugin.Log.LogInfo("已读取元数据缓存: " + _cache.Count + " 条");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("读取元数据缓存失败（将重建）: " + e.Message);
                _cache.Clear();
            }
        }

        public static void Flush()
        {
            if (!_dirty) return;
            try
            {
                var path = SavePaths.SideFile(FileName);
                if (path == null) return;

                var sb = new StringBuilder();
                sb.Append(FormatHeader).Append("\r\n");
                foreach (var meta in _cache.Values) sb.Append(meta.Encode()).Append("\r\n");

                SavePaths.WriteAtomic(path, sb.ToString());
                _dirty = false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("保存元数据缓存失败: " + e.Message);
            }
        }
    }
}
