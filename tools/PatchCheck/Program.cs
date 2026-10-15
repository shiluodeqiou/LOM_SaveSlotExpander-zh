using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace LOM.SaveSlotExpander.PatchCheck
{
    /// <summary>
    /// 静态核对工具：把 LOM_SaveSlotExpander.dll 里的 Harmony 补丁目标，
    /// 逐条对到当前版本的游戏程序集 Mortal.Core.dll 上。
    ///
    /// 游戏更新后跑一下，就能知道本 MOD 还认不认得游戏内部结构，
    /// 不必先启动游戏再看有没有报错。
    ///
    /// 用法（在仓库根目录）：
    ///   dotnet run --project tools/PatchCheck -- &lt;游戏根目录&gt; [插件dll路径]
    /// 省略参数时按默认路径查找。
    /// </summary>
    internal static class Program
    {
        private static int _bad;

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

            string gameRoot = args.Length > 0 ? args[0] : DefaultGameRoot();
            string plugin = args.Length > 1 ? args[1] : "dist/LOM_SaveSlotExpander/LOM_SaveSlotExpander.dll";

            string core = Path.Combine(gameRoot, "Mortal_Data", "Managed", "Mortal.Core.dll");
            if (!File.Exists(core))
            {
                Console.WriteLine("找不到游戏程序集: " + core);
                return 2;
            }
            if (!File.Exists(plugin))
            {
                Console.WriteLine("找不到插件: " + plugin);
                return 2;
            }

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.Combine(gameRoot, "Mortal_Data", "Managed"));
            resolver.AddSearchDirectory(Path.Combine(gameRoot, "BepInEx", "core"));

            var readerParams = new ReaderParameters { AssemblyResolver = resolver, ReadSymbols = false };
            using (var game = AssemblyDefinition.ReadAssembly(core, readerParams))
            using (var mod = AssemblyDefinition.ReadAssembly(plugin, readerParams))
            {
                Console.WriteLine("游戏程序集: " + core);
                Console.WriteLine("插件      : " + plugin);
                Console.WriteLine();

                // 额外参数 --list 类型名：列出该类型的方法与字段，用来人工核对
                int listAt = Array.IndexOf(args, "--list");
                if (listAt >= 0 && listAt + 1 < args.Length)
                {
                    ListType(game, args[listAt + 1]);
                    return 0;
                }

                Console.WriteLine("[Harmony 补丁目标]");
                CheckPatchTargets(mod, game);

                Console.WriteLine();
                Console.WriteLine("[通过 Harmony 访问的私有字段]");
                CheckFields(game);

                Console.WriteLine();
                Console.WriteLine("[方法签名]");
                CheckMethods(game);
            }

            Console.WriteLine();
            if (_bad == 0)
            {
                Console.WriteLine("全部通过：补丁目标与当前游戏版本一致。");
                return 0;
            }

            Console.WriteLine("有 " + _bad + " 项对不上。游戏更新可能改动了内部结构，本 MOD 需要跟进。");
            return 1;
        }

        // ------------------------------------------------------------------

        private static void CheckPatchTargets(AssemblyDefinition mod, AssemblyDefinition game)
        {
            foreach (var type in AllTypes(mod.MainModule))
            {
                foreach (var attr in type.CustomAttributes)
                {
                    if (attr.AttributeType.Name != "HarmonyPatch") continue;

                    var args = attr.ConstructorArguments;
                    if (args.Count == 0)
                    {
                        // [HarmonyPatch] + TargetMethod()，目标由反射决定，这里单独核对
                        Report("（动态目标）" + type.Name, type.Name == "SaveSystem_GetSaveData");
                        continue;
                    }

                    var targetType = args[0].Value as TypeReference;
                    if (targetType == null)
                    {
                        Report("（无法解析目标类型）" + type.Name, false);
                        continue;
                    }

                    string methodName = args.Count > 1 ? args[1].Value as string : null;
                    string label = type.Name + " -> " + targetType.Name
                                   + (methodName != null ? "." + methodName : "");

                    if (methodName == null)
                    {
                        Report(label + "（只指定类型）", true);
                        continue;
                    }

                    TypeReference[] parameterTypes = null;
                    if (args.Count > 2 && args[2].Value is CustomAttributeArgument[] pa)
                        parameterTypes = pa.Select(x => x.Value as TypeReference).ToArray();

                    bool found = FindMethod(game, targetType.Name, methodName, parameterTypes);
                    Report(label + (parameterTypes != null ? "(" + string.Join(", ", parameterTypes.Select(t => t.Name)) + ")" : ""),
                           found);
                }
            }
        }

        private static bool FindMethod(AssemblyDefinition game, string typeName, string methodName, TypeReference[] parameterTypes)
        {
            var type = FindType(game, typeName);
            if (type == null) return false;

            foreach (var m in type.Methods)
            {
                if (m.Name != methodName) continue;
                if (parameterTypes == null) return true;

                if (m.Parameters.Count != parameterTypes.Length) continue;

                bool all = true;
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    if (m.Parameters[i].ParameterType.Name != parameterTypes[i].Name) { all = false; break; }
                }
                if (all) return true;
            }
            return false;
        }

        /// <summary>本 MOD 通过 AccessTools.FieldRefAccess 引用的私有字段。</summary>
        private static readonly (string Type, string Field)[] Fields =
        {
            ("SaveGamePanel", "_slotCount"),
            ("SaveGamePanel", "_saveSlots"),
            ("SaveGamePanel", "_slotContainer"),
            ("SaveGamePanel", "_closeInputButton"),
            ("LoadGamePanel", "_slotCount"),
            ("LoadGamePanel", "_saveSlots"),
            ("LoadGamePanel", "_slotContainer"),
            ("LoadGamePanel", "_closeButton"),
            ("SaveSlotPanel", "_currentData"),
            ("SaveSlotPanel", "_slot"),
            ("SaveSlotPanel", "_titleText"),
            ("SaveSlotPanel", "_timeText"),
            ("LoadSlotPanel", "_currentData"),
            ("LoadSlotPanel", "_slot"),
            ("LoadSlotPanel", "_titleText"),
            ("LoadSlotPanel", "_timeText"),
            ("RecentSaveSlotPanel", "_slot"),
            ("RecentSaveSlotPanel", "_titleText"),
            ("RecentSaveSlotPanel", "_timeText"),
        };

        private static void CheckFields(AssemblyDefinition game)
        {
            foreach (var entry in Fields)
            {
                var type = FindType(game, entry.Type);
                bool found = type != null && type.Fields.Any(f => f.Name == entry.Field);
                Report(entry.Type + "." + entry.Field, found);
            }
        }

        /// <summary>在代码里按名字取用的方法。</summary>
        private static readonly (string Type, string Method, string[] Args)[] Methods =
        {
            ("SaveSystem", "GetSaveDataFile", new[] { "String" }),
            ("SaveSystem", "GetSaveData", new[] { "String" }),
            ("SaveGamePanel", "SetButtonNavigation", null),
            ("LoadGamePanel", "SetButtonNavigation", null),
            ("SaveSystem", "SaveGameData", new[] { "String" }),
            ("SaveSystem", "NewGameData", null),
            ("SaveSystem", "NewActGameData", null),
            ("SaveSystem", "DeleteSaveData", new[] { "String" }),
            ("LoadGamePanel", "OpenDeletePanel", null),
            // 只有 SaveSlotPanel 有 SelectButton（LoadSlotPanel 没有，本 MOD 也没用它）
            ("SaveSlotPanel", "SelectButton", null),
        };

        private static void CheckMethods(AssemblyDefinition game)
        {
            foreach (var entry in Methods)
            {
                var type = FindType(game, entry.Type);
                if (type == null) { Report(entry.Type + "." + entry.Method, false); continue; }

                bool found = type.Methods.Any(m =>
                    m.Name == entry.Method &&
                    (entry.Args == null || (m.Parameters.Count == entry.Args.Length &&
                                            m.Parameters.Select(p => p.ParameterType.Name).SequenceEqual(entry.Args))));
                Report(entry.Type + "." + entry.Method
                       + (entry.Args != null ? "(" + string.Join(", ", entry.Args) + ")" : ""), found);
            }

            // GetSaveData 必须有一个返回 GameSave 的重载，否则动态目标选不出来
            var save = FindType(game, "SaveSystem");
            bool hasGameSaveOverload = save != null && save.Methods.Any(m =>
                m.Name == "GetSaveData" && m.ReturnType.Name == "GameSave" &&
                m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "String");
            Report("SaveSystem.GetSaveData(String) 返回 GameSave", hasGameSaveOverload);
        }

        // ------------------------------------------------------------------

        /// <summary>列出某个类型的成员，供人工核对使用。</summary>
        private static void ListType(AssemblyDefinition game, string typeName)
        {
            var type = FindType(game, typeName);
            if (type == null)
            {
                Console.WriteLine("找不到类型: " + typeName);
                return;
            }

            Console.WriteLine("类型 " + type.FullName + (type.BaseType != null ? " : " + type.BaseType.Name : ""));

            Console.WriteLine();
            Console.WriteLine("[字段]");
            foreach (var f in type.Fields)
                Console.WriteLine("  " + f.FieldType.Name + " " + f.Name);

            Console.WriteLine();
            Console.WriteLine("[方法]");
            foreach (var m in type.Methods)
                Console.WriteLine("  " + m.ReturnType.Name + " " + m.Name
                                  + "(" + string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
        }

        private static void Report(string label, bool ok)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "OK  " : "缺失") + " " + label);
        }

        private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
        {
            foreach (var t in module.Types)
            {
                yield return t;
                foreach (var n in Nested(t)) yield return n;
            }
        }

        private static IEnumerable<TypeDefinition> Nested(TypeDefinition type)
        {
            foreach (var t in type.NestedTypes)
            {
                yield return t;
                foreach (var n in Nested(t)) yield return n;
            }
        }

        private static TypeDefinition FindType(AssemblyDefinition asm, string name)
        {
            return AllTypes(asm.MainModule).FirstOrDefault(t => t.Name == name);
        }

        private static string DefaultGameRoot()
        {
            var env = Environment.GetEnvironmentVariable("LOM_GAME_ROOT");
            if (!string.IsNullOrEmpty(env)) return env;
            return @"C:\Program Files (x86)\Steam\steamapps\common\LegendOfMortal";
        }
    }
}
