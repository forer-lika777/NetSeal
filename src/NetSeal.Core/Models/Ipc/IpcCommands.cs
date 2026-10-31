using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public static class IpcCommands
{
    public const string CheckServiceStatus = "CHECK_STATUS";
    public const string GetAppSettingsKeyValue = "GET";
    public const string SetAppSettingsKeyValue = "SET";
    public const string RasConnect = "RAS_CONNECT";
    public const string RasDisconnect = "RAS_DISCONNECT";
}
