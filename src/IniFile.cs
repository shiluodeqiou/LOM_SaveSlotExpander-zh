using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 读写单节 ini（key=value）的最小实现。
    ///
    /// 原来用 UnityEngine.JsonUtility，但它不能正确序列化 BepInEx 动态加载的
    /// 程序集里的类型，会把字段全部忽略、只写出 "{}"，保存功能实际上没生效。
    /// 因此换成了不依赖外部的自前实现，顺带还变成了可以用文本编辑器直接改的格式。
    ///
    /// 值里不能带换行（由调用方去掉）。
    /// 值里可以有 '='（只把第一个 '=' 当分隔符）。
    /// </summary>
    internal static class IniFile
    {
        public static Dictionary<string, string> Read(string path)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            // BOM 有无由 StreamReader 判断
            foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line[0] == '#' || line[0] == ';') continue;
                if (line[0] == '[') continue;                 // 节标题跳过

                int sep = line.IndexOf('=');
                if (sep <= 0) continue;

                var key = line.Substring(0, sep).Trim();
                var value = line.Substring(sep + 1).Trim();
                if (key.Length == 0) continue;

                result[key] = value;
            }
            return result;
        }

        /// <summary>
        /// 组装 key=value 形式的正文。键会排序，保证顺序稳定。
        /// header 是放在开头的说明（每一行都要自己带 #）。
        ///
        /// 这里不做文件写入，是为了能把这个函数单独拿出来，
        /// 脱离游戏做往返测试。
        /// </summary>
        public static string Format(string header, IDictionary<string, string> entries)
        {
            var keys = new List<string>(entries.Keys);
            keys.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header))
            {
                sb.Append(header);
                if (!header.EndsWith("\n")) sb.Append("\r\n");
                sb.Append("\r\n");
            }

            foreach (var key in keys)
            {
                var value = entries[key] ?? "";
                sb.Append(key).Append(" = ").Append(Sanitize(value)).Append("\r\n");
            }

            return sb.ToString();
        }

        /// <summary>把会破坏行结构的字符压掉。</summary>
        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\r", "").Replace("\n", " ").Trim();
        }
    }
}
