using System;
using System.Globalization;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 列表显示所需的最小信息。
    ///
    /// 不依赖游戏也不依赖 Unity，所以可以把这个类型和 Encode/Decode
    /// 单独拿出来，脱离游戏做往返测试。
    /// </summary>
    internal class SlotMeta
    {
        public string Slot;
        public long FileTicks;
        public long FileSize;

        public long TimeTick;
        public bool IsDemo;
        public bool NewGamePlus;
        public int CurrentYear;
        public int CurrentMonth;
        public int CurrentStage;

        private const char Sep = '|';

        public string Encode()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(Sep.ToString(), new[]
            {
                Slot,
                FileTicks.ToString(c),
                FileSize.ToString(c),
                TimeTick.ToString(c),
                IsDemo ? "1" : "0",
                NewGamePlus ? "1" : "0",
                CurrentYear.ToString(c),
                CurrentMonth.ToString(c),
                CurrentStage.ToString(c),
            });
        }

        /// <summary>坏掉的行返回 null（调用方直接跳过）。</summary>
        public static SlotMeta Decode(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var f = line.Split(Sep);
            if (f.Length != 9) return null;
            if (string.IsNullOrEmpty(f[0])) return null;

            var c = CultureInfo.InvariantCulture;
            try
            {
                return new SlotMeta
                {
                    Slot = f[0],
                    FileTicks = long.Parse(f[1], c),
                    FileSize = long.Parse(f[2], c),
                    TimeTick = long.Parse(f[3], c),
                    IsDemo = f[4] == "1",
                    NewGamePlus = f[5] == "1",
                    CurrentYear = int.Parse(f[6], c),
                    CurrentMonth = int.Parse(f[7], c),
                    CurrentStage = int.Parse(f[8], c),
                };
            }
            catch (FormatException) { return null; }
            catch (OverflowException) { return null; }
        }
    }
}
