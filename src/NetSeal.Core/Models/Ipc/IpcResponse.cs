using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public sealed class IpcResponse
{
    public bool Success { get; set; }
    public string? Value { get; set; }   // GET 成功时的值
    public string? Error { get; set; }   // 失败原因

    public static IpcResponse Ok(string? value = null) => new() { Success = true, Value = value };
    public static IpcResponse Fail(string message) => new() { Success = false, Error = message };
}
