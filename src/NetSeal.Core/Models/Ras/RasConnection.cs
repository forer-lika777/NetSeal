namespace NetSeal.Core.Models;

public class RasConnection
{
    public string? PhoneBookName { get; set; }
    public required string EntryName { get; set; }
    public required IntPtr Handle { get; set; }
}
