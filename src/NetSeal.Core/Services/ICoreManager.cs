using NetSeal.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services;

public interface ICoreManager
{
    public Task<NetworkStatus> GetNetworkStatusAsync();
    public event EventHandler? NetworkStatusChanged;
}
