using System.Text.Json;

namespace NetSeal.Core.Models;

public sealed class IpcResponse
{
    public bool Success { get; set; }
    public string? Value { get; set; }   // GET 成功时的值
    public string? Error { get; set; }   // 失败原因

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static IpcResponse Ok(string? value = null) => new() { Success = true, Value = value };
    public static string SerializeOk(string? value = null) => JsonSerializer.Serialize(Ok(), JsonOptions);
    public static IpcResponse Fail(string message) => new() { Success = false, Error = message };
    public static string SerializeFail(string message) => JsonSerializer.Serialize(Fail(message), JsonOptions);
}
