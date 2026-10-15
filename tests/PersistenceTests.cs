using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LOM.SaveSlotExpander;

internal static class PersistenceTests
{
    private static int _failed;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  OK  " : "  NG  ") + what);
        if (!ok) _failed++;
    }

    public static int Main()
    {
        // 没有终端的环境（CI 之类）可能设置不了，忽略掉即可
        try { Console.OutputEncoding = Encoding.UTF8; } catch (Exception) { }

        var dir = Path.Combine(Path.GetTempPath(), "lom_sse_tests");
        Directory.CreateDirectory(dir);

        TestIniRoundTrip(dir);
        TestIniEdgeCases(dir);
        TestMetaRoundTrip();
        TestMetaCorruption();
        _failed += SortTests.Run();

        Console.WriteLine();
        Console.WriteLine(_failed == 0 ? "全部成功" : _failed + " 项失败");
        return _failed == 0 ? 0 : 1;
    }

    private static void TestIniRoundTrip(string dir)
    {
        Console.WriteLine("[ini 往返] 含实际会输入的内容");
        var src = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["001"] = "一周目·善人路线√",              // 玩家实际反馈过的写法
            ["017"] = "第二年十二月中旬的存档",
            ["018"] = "啊啊啊啊啊啊啊啊啊啊啊啊啊啊",
            ["100"] = "a = b 这样的值",                 // 值里含 =
            ["999"] = "  前后有空格  ",
            ["050"] = "【符号】①②③ ~!@#$%^&*()_+",
            ["051"] = "表情🎮与代理对𠮷",
        };

        var path = Path.Combine(dir, "comments.ini");
        File.WriteAllText(path, IniFile.Format("# 测试", src), new UTF8Encoding(true));

        var back = IniFile.Read(path);

        Check(back.Count == src.Count, "条数一致 (" + back.Count + "/" + src.Count + ")");
        Check(back.ContainsKey("001") && back["001"] == "一周目·善人路线√", "「一周目·善人路线√」完整还原");
        Check(back["017"] == src["017"], "中文长值");
        Check(back["018"] == src["018"], "同一字符重复");
        Check(back["100"] == "a = b 这样的值", "值里的 = 得以保留");
        Check(back["999"] == "前后有空格", "前后的空格被去掉（符合设计）");
        Check(back["050"] == src["050"], "符号类");
        Check(back["051"] == src["051"], "表情与代理对");

        // 是否带 BOM 写成 UTF-8
        var raw = File.ReadAllBytes(path);
        Check(raw.Length > 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF, "以带 BOM 的 UTF-8 保存");
        Check(raw.Length > 20, "内容非空 (" + raw.Length + " bytes)");
    }

    private static void TestIniEdgeCases(string dir)
    {
        Console.WriteLine("[ini 异常情况]");
        var path = Path.Combine(dir, "weird.ini");
        File.WriteAllText(path,
            "# 注释行\r\n" +
            "; 这也是注释\r\n" +
            "[Section]\r\n" +
            "\r\n" +
            "  007  =  值前后有空格  \r\n" +
            "坏掉的行\r\n" +
            "=只有值\r\n" +
            "008=\r\n",
            new UTF8Encoding(true));

        var map = IniFile.Read(path);
        Check(map.Count == 2, "跳过注释、节标题和坏行 (" + map.Count + " 条)");
        Check(map.ContainsKey("007") && map["007"] == "值前后有空格", "去掉键和值前后的空格");
        Check(map.ContainsKey("008") && map["008"] == "", "允许空值");
        Check(!map.ContainsKey(""), "键为空的行丢弃");

        Check(IniFile.Read(Path.Combine(dir, "不存在.ini")).Count == 0, "文件不存在时返回空");
        Check(IniFile.Sanitize("含\r\n换行") == "含 换行", "换行被去掉");
    }

    private static void TestMetaRoundTrip()
    {
        Console.WriteLine("[元数据 往返]");
        var meta = new SlotMeta
        {
            Slot = "042",
            FileTicks = 638900000000000000L,
            FileSize = 65195,
            TimeTick = 638899999999999999L,
            IsDemo = false,
            NewGamePlus = true,
            CurrentYear = 2,
            CurrentMonth = 12,
            CurrentStage = 1,
        };

        var back = SlotMeta.Decode(meta.Encode());
        Check(back != null, "能解码");
        Check(back.Slot == "042", "存档位名");
        Check(back.FileTicks == meta.FileTicks, "修改时刻（long 不丢精度）");
        Check(back.FileSize == meta.FileSize, "文件大小");
        Check(back.TimeTick == meta.TimeTick, "存档时刻");
        Check(back.IsDemo == false && back.NewGamePlus, "布尔值");
        Check(back.CurrentYear == 2 && back.CurrentMonth == 12 && back.CurrentStage == 1, "年月旬");
    }

    private static void TestMetaCorruption()
    {
        Console.WriteLine("[元数据 异常情况]");
        Check(SlotMeta.Decode(null) == null, "null");
        Check(SlotMeta.Decode("") == null, "空字符串");
        Check(SlotMeta.Decode("001|2|3") == null, "项数不足的行");
        Check(SlotMeta.Decode("001|x|3|4|0|0|1|2|3") == null, "不是数字的项");
        Check(SlotMeta.Decode("|1|2|3|0|0|1|2|3") == null, "存档位名为空");
        Check(SlotMeta.Decode("001|99999999999999999999|2|3|0|0|1|2|3") == null, "数值溢出");
    }
}
