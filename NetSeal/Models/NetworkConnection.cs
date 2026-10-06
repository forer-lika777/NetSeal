using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Models;

public class NetworkConnection
{
    public string Name { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public bool IsConnected { get; set; } = false;
    public bool IsPrivate { get; set; } = false;
    public bool IsWireless { get; set; } = false;
}
