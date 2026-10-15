using System;
using System.Collections.Generic;
using LOM.SaveSlotExpander;

/// <summary>
/// 把排列顺序的规则钉死的测试。
/// 它直接决定界面上看到的样子，所以改规则时必须同步改这里。
/// </summary>
internal static class SortTests
{
    private static int _failed;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  OK  " : "  NG  ") + what);
        if (!ok) _failed++;
    }

    /// <summary>把重排结果按存档位编号拼成字符串返回。</summary>
    private static string Result(SlotKey[] items, bool recentFirst, bool emptySlotFirst)
    {
        var order = SlotOrder.Resolve(items, recentFirst, emptySlotFirst);
        var names = new List<string>();
        foreach (var i in order) names.Add(items[i].Slot);
        return string.Join(",", names.ToArray());
    }

    private static SlotKey Saved(string slot, long tick)
    {
        return new SlotKey { Slot = slot, HasData = true, TimeTick = tick };
    }

    private static SlotKey Empty(string slot)
    {
        return new SlotKey { Slot = slot, HasData = false, TimeTick = 0L };
    }

    public static int Run()
    {
        _failed = 0;
        Console.WriteLine("[排列顺序]");

        // 001 和 003 有存档（003 更近），002 和 004 是空的
        var items = new[]
        {
            Saved("001", 100),
            Empty("002"),
            Saved("003", 300),
            Empty("004"),
        };

        Check(Result(items, false, false) == "001,002,003,004", "默认按存档位编号排");
        Check(Result(items, false, true) == "001,002,003,004", "默认不把空位提前");

        // 读档界面：最前面是最近保存的那一档
        Check(Result(items, true, false) == "003,001,002,004",
              "读档界面按时间从新到旧，空位按编号排在后");

        // 存档界面：最前面是新建存档用的空位，第二个才是最近保存的那一档
        Check(Result(items, true, true) == "002,003,001,004",
              "存档界面最前面是空位，第二个是最近保存的那一档");

        // 没有空位时就没有可提前的
        var noEmpty = new[] { Saved("001", 100), Saved("002", 300), Saved("003", 200) };
        Check(Result(noEmpty, true, true) == "002,003,001", "没有空位时只按时间从新到旧");

        // 全是空位时保持编号顺序
        var allEmpty = new[] { Empty("003"), Empty("001"), Empty("002") };
        Check(Result(allEmpty, true, true) == "001,002,003", "全为空时按编号排");

        // 时间相同时按编号排，保证顺序稳定
        var sameTime = new[] { Saved("005", 500), Saved("002", 500), Saved("009", 500) };
        Check(Result(sameTime, true, false) == "002,005,009", "时间相同时按编号排，顺序稳定");

        // 位数变多也不会因字符串比较而错乱（三位零填充）
        var many = new[] { Empty("100"), Empty("020"), Empty("003") };
        Check(Result(many, false, false) == "003,020,100", "三位零填充下字符串比较等于编号顺序");

        // 异常情况
        Check(SlotOrder.Resolve(null, true, true).Length == 0, "null 返回空");
        Check(SlotOrder.Resolve(new SlotKey[0], true, true).Length == 0, "空数组返回空");

        // 重排后元素不增不减
        var order = SlotOrder.Resolve(items, true, true);
        var seen = new HashSet<int>(order);
        Check(order.Length == items.Length && seen.Count == items.Length,
              "原有元素不重不漏各出现一次");

        return _failed;
    }
}
