using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public sealed class IpcResponse
{
    public bool Success { get; set; }
    public string? Value { get; set; }   // GET 成功时的值
    public string? Error { get; set; }   // 失败原因

    public static IpcResponse Ok(string? value = null) => new() { Success = true, Value = value };
    public static string SerializeOk(string? value = null) => JsonSerializer.Serialize(Ok(value), IpcResponseJsonContext.Default.IpcResponse);
    public static IpcResponse Fail(string message) => new() { Success = false, Error = message };
    public static string SerializeFail(string message) => JsonSerializer.Serialize(Fail(message), IpcResponseJsonContext.Default.IpcResponse);
}

[JsonSerializable(typeof(IpcResponse))]
public partial class IpcResponseJsonContext : JsonSerializerContext
{

}