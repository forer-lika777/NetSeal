using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.Pipe;

public sealed class PipeRequest
{
    public string Cmd { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Value { get; set; }
}