using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Models;

public class RasConnection
{
    public required string EntryName { get; set; }
    public required IntPtr Handle { get; set; }
}
