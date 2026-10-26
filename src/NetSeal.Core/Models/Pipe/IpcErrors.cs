using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public static class IpcErrors
{
    public const string UnknownCommand = "UNKNOWN_COMMAND";
    public const string Internal = "INTERNAL";            // 兜底，不暴露细节
}