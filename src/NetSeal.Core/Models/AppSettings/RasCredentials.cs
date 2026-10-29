using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public class RasCredentials
{
    public RasCredentials()
    {

    }

    public RasCredentials(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[JsonSerializable(typeof(RasCredentials))]
public partial class RasCredentialsJsonContext : JsonSerializerContext
{

}
