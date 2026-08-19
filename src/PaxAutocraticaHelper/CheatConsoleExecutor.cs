using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using EFAS.GameTime;
using Il2CppInterop.Runtime.InteropTypes;
using Multiverse.Console;
using UnityEngine;

namespace PaxAutocraticaHelper;

/// <summary>
/// 游戏作弊控制台（CheatConsole）命令执行器。
/// 相比 v0.3.0：MethodInfo 按签名缓存，避免每次调用全量反射。
/// </summary>
internal static class CheatConsoleExecutor
{
    /// <summary>方法缓存：方法名 -> (参数个数 -> MethodInfo)</summary>
    private static readonly Dictionary<string, Dictionary<int, MethodInfo>> _cache = new();

    /// <summary>上次执行的命令与时间（相同命令限频用）</summary>
    private static string? _lastCommand;
    private static float _lastCmdTime;

    /// <summary>执行 CheatConsole.<paramref name="methodName"/>，参数自动做类型转换 */
    internal static bool RunCommand(string methodName, object[]? args)
    {
        try
        {
            var self = ConsoleLineHelper.Self;
            if (self == null)
            {
                PaxPlugin.Log.LogError("RunCommand: ConsoleLineHelper.Self is null");
                return false;
            }

            // 通过 Dics 找到执行目标对象（原版逻辑：MethodTarget.obj 持有对象实例）
            Il2CppObjectBase? rawObj = null;
            foreach (var kv in self.Dics)
            {
                if (kv.Value.Name == "CheatConsole." + methodName)
                {
                    rawObj = kv.Value.obj as Il2CppObjectBase;
                    break;
                }
            }
            if (rawObj == null)
            {
                // 兜底：Dics 里 Name 可能不含前缀
                foreach (var kv in self.Dics)
                {
                    if (kv.Value.Name == methodName)
                    {
                        rawObj = kv.Value.obj as Il2CppObjectBase;
                        break;
                    }
                }
            }
            if (rawObj == null)
            {
                PaxPlugin.Log.LogError($"RunCommand: target object not found for {methodName}");
                return false;
            }

            var method = ResolveMethod(methodName, args);
            if (method == null)
            {
                PaxPlugin.Log.LogError($"RunCommand: method not found: {methodName}({args?.Length ?? 0} args)");
                return false;
            }

            // 关键：必须 Cast 成 CheatConsole 类型再 Invoke，
            // 否则反射 TargetException: Object does not match target type
            var console = rawObj.Cast<CheatConsole>();
            var converted = ConvertArgs(args, method);
            method.Invoke(console, converted);
            PaxPlugin.Log.LogInfo($"CheatConsole.{methodName} executed.");
            return true;
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"RunCommand({methodName}) failed: {ex}");
            return false;
        }
    }

    /// <summary>解析（并缓存）目标方法：优先精确参数个数，参数类型宽松匹配 */
    private static MethodInfo? ResolveMethod(string methodName, object[]? args)
    {
        var argCount = args?.Length ?? 0;
        if (_cache.TryGetValue(methodName, out var byCount) && byCount.TryGetValue(argCount, out var hit))
        {
            return hit;
        }

        var methods = typeof(CheatConsole).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == methodName && m.GetParameters().Length == argCount)
            .ToList();
        var method = methods.FirstOrDefault() ?? methods.FirstOrDefault(m => m.GetParameters().Length == argCount);

        _cache[methodName] = new Dictionary<int, MethodInfo> { [argCount] = method! };
        return method;
    }

    /// <summary>按参数类型转换参数（bool/int/float/string 互相兼容） */
    private static object[] ConvertArgs(object[]? args, MethodInfo method)
    {
        if (args == null || args.Length == 0) return Array.Empty<object>();
        var parameters = method.GetParameters();
        var result = new object[args.Length];
        for (var i = 0; i < args.Length && i < parameters.Length; i++)
        {
            result[i] = Coerce(args[i], parameters[i].ParameterType);
        }
        return result;
    }

    private static object Coerce(object value, Type target)
    {
        if (target == typeof(bool))
        {
            return value switch
            {
                bool b => b,
                int i => i != 0,
                float f => f != 0f,
                _ => value.ToString() == "1" || string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase)
            };
        }
        if (target == typeof(int))
        {
            return value switch
            {
                int i => i,
                float f => (int)f,
                _ => int.TryParse(value.ToString(), out var i) ? i : 0
            };
        }
        if (target == typeof(float))
        {
            return value switch
            {
                float f => f,
                int i => (float)i,
                _ => float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f
            };
        }
        if (target == typeof(string))
        {
            return value.ToString() ?? "";
        }
        return value;
    }

    /// <summary>设置时间倍速：游戏时间（GameTimeManager）与 Unity 时间一起改。供快捷键调用。</summary>
    internal static void SetTimeScale(float scale)
    {
        try
        {
            var gtm = GameTimeManager.Instance;
            if (gtm != null)
            {
                gtm.TimeScale = scale;
                gtm.SettingsTimeScale = scale;
                PaxPlugin.Log.LogInfo($"GameTimeManager.TimeScale set to {scale}");
            }
        }
        catch (Exception ex)
        {
            PaxPlugin.Log.LogError($"GameTimeManager set failed: {ex}");
        }
        Time.timeScale = scale;
        PaxPlugin.Log.LogInfo($"Unity Time.timeScale set to {scale}");
    }

    /// <summary>
    /// 解析面板/快捷键传入的命令字符串。
    /// "SetTimeScale 5" 特殊处理（游戏时间 + Unity 时间）；其余转为 CheatConsole 调用。
    /// </summary>
    internal static void Exec(string command)
    {
        // 限频：相同命令 0.3 秒内重复忽略（原版行为）；
        // 不同命令不限制——否则快捷键连按（如 Ctrl+5 后立刻 Ctrl+6）会被吞掉
        var now = Time.realtimeSinceStartup;
        if (command == _lastCommand && now - _lastCmdTime < 0.3f) return;
        _lastCommand = command;
        _lastCmdTime = now;

        if (command.StartsWith("SetTimeScale ", StringComparison.Ordinal))
        {
            var text = command.Substring("SetTimeScale ".Length);
            var scale = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 1f;
            SetTimeScale(scale);
            return;
        }

        var parts = command.Split(' ');
        object[]? args = null;
        if (parts.Length > 1)
        {
            args = new object[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
            {
                args[i - 1] = parts[i];
            }
        }
        RunCommand(parts[0], args);
    }
}
