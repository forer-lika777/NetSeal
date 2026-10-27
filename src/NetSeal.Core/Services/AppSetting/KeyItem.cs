using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Services.AppSetting;

/// <summary>
/// 用于存储和读取配置项的键值对容器。
/// 接受直接存储基础类型，也接受支持 JSON 序列化的自定义类型。
/// 接受的的基础类型：bool, string, int, uint, long, ulong, float, double, char, DateTime, TimeSpan, Guid, byte[].
/// </summary>
/// <typeparam name="T">存储的值的类型。</typeparam>
public class KeyItem<T> : KeyItemBase<T>, IKeyItem<T> where T : notnull
{
    public KeyItem(ISettingsStore settingsStore, string name, T defaultValue, JsonSerializerContext? context = null) : base(settingsStore, name, defaultValue, context)
    {
        value = defaultValue;
    }

    public async Task<T> GetValueAsync(CancellationToken ct = default)
    {
        string? raw = await settingsStore.GetAsync(Name, ct);

        if (raw is null)
        {
            Debug.WriteLine($"设置 '{Name}' 不存在，使用默认值。");
            return this.value;
        }

        T? loaded = GetTypedValue(raw);

        if (loaded is null)
        {
            Debug.WriteLine($"加载设置项 '{Name}' 时失败。");
            return this.value;
        }

        value = loaded;

        return loaded;
    }

    public async Task SetValueAsync(T value, CancellationToken ct = default)
    {
        if (EqualityComparer<T>.Default.Equals(this.value, value))
            return;

        string raw = GetStringValue(value);
        await settingsStore.SetAsync(Name, raw, ct);
        this.value = value;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        string raw = GetStringValue(value);

        await settingsStore.SetAsync(Name, raw, ct);
    }
}

public class SecureKeyItem<T> : KeyItemBase<T>, IKeyItem<T> where T : notnull
{
    public SecureKeyItem(ISettingsStore settingsStore, string name, T defaultValue, JsonSerializerContext? Context = null) : base(settingsStore, name, defaultValue, Context)
    {
        value = defaultValue;
    }

    public async Task<T> GetValueAsync(CancellationToken ct = default)
    {
        string? raw = await settingsStore.GetAsync(Name, ct);

        if (raw is null)
        {
            Debug.WriteLine($"设置 '{Name}' 不存在，将使用默认值。");
            return this.value;
        }

        string decryptedValue;

        try
        {
            decryptedValue = Decrypt(raw);
        }
        catch (CryptographicException ex)
        {
            Debug.WriteLine($"设置项 '{Name}' 解密失败，将使用默认值: {ex.Message}");
            return this.value;
        }

        T? loaded = GetTypedValue(decryptedValue);

        if (loaded is null)
        {
            Debug.WriteLine($"加载设置项 '{Name}' 时失败。");
            return this.value;
        }

        return loaded;
    }

    public async Task SetValueAsync(T value, CancellationToken ct = default)
    {
        if (EqualityComparer<T>.Default.Equals(this.value, value))
            return;

        string raw = GetStringValue(value);
        string encrypted = Encrypt(raw);
        await settingsStore.SetAsync(Name, encrypted, ct);
        this.value = value;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        string raw = GetStringValue(value);

        string encrypted = Encrypt(raw);

        await settingsStore.SetAsync(Name, encrypted, ct);
    }

    private static string Encrypt(string data)
    {
        if (string.IsNullOrEmpty(data))
            return string.Empty;

        byte[] plainBytes = Encoding.UTF8.GetBytes(data);
        byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

        return Convert.ToBase64String(protectedBytes);
    }

    private static string Decrypt(string data)
    {
        if (string.IsNullOrEmpty(data))
            return string.Empty;

        byte[] protectedBytes;
        try
        {
            protectedBytes = Convert.FromBase64String(data);
        }
        catch (FormatException ex)
        {
            Debug.WriteLine($"Base64 解码失败，数据格式无效: {ex.Message}");
            throw;
        }

        byte[] clearBytes;
        try
        {
            clearBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException ex)
        {
            Debug.WriteLine($"解密失败，数据可能损坏或密钥不匹配: {ex.Message}");
            throw;
        }

        return Encoding.UTF8.GetString(clearBytes);
    }
}