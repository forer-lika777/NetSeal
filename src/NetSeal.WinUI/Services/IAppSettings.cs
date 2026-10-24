using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NetSeal.Core.Models;

namespace NetSeal.Services;

public interface IAppSettings
{
    public IKeyItem<bool> HasSavedNetworkAuth { get; }
    public IKeyItem<Core.Models.Network> SelectedNetwork { get; }
    public IKeyItem<bool> AutoConnect { get; }
    public IKeyItem<string> AccountId { get; }
    public IKeyItem<string> Password { get; }
}

public interface IKeyItem<T> where T : notnull
{
    public string Name { get; }
    public T Value { get; set; }
    public void Load();
    public void Save();
}
