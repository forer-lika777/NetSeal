using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Services.AppSetting;

/// <summary>
/// 共享的键值项基类。处理值存储、类型判断、JSON 序列化/反序列化。
/// 不包含任何存储介质逻辑（本地或远程）。
/// </summary>
public abstract class KeyItemBase<T> where T : notnull
{
    protected readonly ISettingsStore settingsStore;

    protected readonly JsonSerializerContext? Context;

    protected T value;

    public string Name { get; }

    protected KeyItemBase(ISettingsStore settingsStore, string name, T defaultValue, JsonSerializerContext? Context = null)
    {
        this.settingsStore = settingsStore;
        Name = name;
        value = defaultValue;
        this.Context = Context;

        if (!IsDirectlySupported && Context == null)
            throw new ArgumentNullException(nameof(Context), $"使用非直接支持类型 {typeof(T).Name} 时，必须提供 JSON Serialize Context。");
    }

    /// <summary>
    /// 判断当前类型是否支持直接转换（不需要走 JSON）
    /// </summary>
    protected static readonly bool IsDirectlySupported =
        typeof(T) == typeof(bool) ||
        typeof(T) == typeof(string) ||
        typeof(T) == typeof(int) ||
        typeof(T) == typeof(uint) ||
        typeof(T) == typeof(long) ||
        typeof(T) == typeof(ulong) ||
        typeof(T) == typeof(float) ||
        typeof(T) == typeof(double) ||
        typeof(T) == typeof(char);

    protected T? GetTypedValue(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return default;

        try
        {
            if (IsDirectlySupported)
                return (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);

            object? deserialized = JsonSerializer.Deserialize(raw, typeof(T), Context!);

            if (deserialized is T typed)
                return typed;

            return default;
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"加载设置 '{Name}' 失败。类型：{typeof(T).Name}，序列化错误信息: {jsonEx.Message}");
            throw;
        }
    }

    protected string GetStringValue(T value)
    {
        try
        {
            string stringValue;

            if (IsDirectlySupported)
            {
                stringValue = Convert.ToString(value, CultureInfo.InvariantCulture) ??
                    throw new NullReferenceException($"无法转换的 null 引用：{typeof(T).Name}。");
            }
            else
            {
                stringValue = JsonSerializer.Serialize(value, typeof(T), Context!);
            }

            return stringValue;
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"保存设置 '{Name}' 失败。类型：{typeof(T).Name}，序列化错误信息: {jsonEx.Message}");
            throw;
        }
    }
}
