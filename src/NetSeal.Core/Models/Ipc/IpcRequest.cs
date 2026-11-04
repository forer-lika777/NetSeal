using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public sealed class IpcRequest
{
    public string Cmd { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Value { get; set; }
}

[JsonSerializable(typeof(IpcRequest))]
public partial class IpcRequestJsonContext : JsonSerializerContext
{

}