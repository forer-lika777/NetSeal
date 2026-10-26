using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public class AutoConnectPolicy
{
    public bool ReconnectOnDisconnect { get; set; } = false;
    public bool ReconnectOnWake { get; set; } = true;
    public bool AutoConnectOnStartup { get; set; } = true;
}

[JsonSerializable(typeof(AutoConnectPolicy))]
public partial class AutoConnectPolicyJsonContext : JsonSerializerContext
{

}