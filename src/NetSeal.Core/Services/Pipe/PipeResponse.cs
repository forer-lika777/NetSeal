using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.Pipe;

public sealed class PipeResponse
{
    public bool Ok { get; set; }
    public string? Value { get; set; }   // GET 成功时的值
    public string? Error { get; set; }   // 失败原因
}
