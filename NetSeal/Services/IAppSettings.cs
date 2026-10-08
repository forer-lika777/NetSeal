using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace NetSeal.Services;

public interface IAppSettings
{
    public IKeyItem<bool> HasSavedNetworkAuth { get; }
    public IKeyItem<Models.Network> SelectedNetwork { get; }
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
