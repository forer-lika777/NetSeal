using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace NetSeal.Core.Services.AppSetting;

public class LocalSettingsStore : ISettingsStore
{
    private readonly string filePath;
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public LocalSettingsStore()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        filePath = Path.Combine(folder, "NetSeal", "settings.json");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
    }

    public async Task<string?> GetAsync(string name, CancellationToken ct = default)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            if (!File.Exists(filePath))
                return null;

            string json;

            // 读取整个 JSON 文件
            try
            { 
                json = await File.ReadAllTextAsync(filePath, ct); 
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"设置文件损坏: {ex.Message}"); return null;
            }

            // 解析成字典
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            // 返回指定的 key 的 value
            if (dict != null && dict.TryGetValue(name, out var value))
            {
                return value;
            }
            return null;
        }
        catch (JsonException)
        {
            // JSON 格式损坏，返回 null
            return null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task SetAsync(string name, string value, CancellationToken ct = default)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            Dictionary<string, string> dict = new();

            // 1. 先读出旧的配置（防止把别的 key 覆盖掉）
            if (File.Exists(filePath))
            {
                try
                {
                    var oldJson = await File.ReadAllTextAsync(filePath, ct);
                    var oldDict = JsonSerializer.Deserialize<Dictionary<string, string>>(oldJson);
                    if (oldDict != null)
                        dict = oldDict;
                }
                catch (JsonException) { /* 旧文件坏了，直接用新字典 */ }
            }

            // 2. 更新指定的 key
            dict[name] = value;

            // 3. 序列化成 JSON
            var newJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });

            // 4. 原子写入：先写临时文件，再替换原文件（防止断电导致文件损坏）
            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, newJson, ct);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
