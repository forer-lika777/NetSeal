using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public class AutoConnectPolicy
{
    public bool ReconnectOnDisconnect { get; set; }
    public bool ReconnectOnWake { get; set; }
    public bool AutoConnectOnStartup { get; set; }
}
