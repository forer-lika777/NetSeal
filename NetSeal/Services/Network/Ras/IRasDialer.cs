using System;
using System.Collections.Generic;
using System.Text;
using static NetSeal.Services.Network.Ras.NativeMethods;

namespace NetSeal.Services.Network.Ras;

public interface IRasDialer
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="phoneBook"></param>
    /// <param name="entryName"></param>
    /// <param name="userName"></param>
    /// <param name="password"></param>
    public void Connect(string? phoneBook, string entryName, string userName, string password);
    public void Disconnect(string entryName);
    public RASCONN? GetActivePppoeConnection();
    public List<RASENTRYNAME> ListAllEntries(string? phoneBook);
    public void CreateEntry(string? phoneBook, string entryName, string userName, string password);
    public void RemoveEntry(string? phoneBook, string entryName);
    public bool EntryExists(string? phoneBook, string entryName);
}
