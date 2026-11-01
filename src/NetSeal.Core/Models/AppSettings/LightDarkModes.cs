using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models;

public enum LightDarkModes
{
    FollowSystem,
    Light,
    Dark,
}

[JsonSerializable(typeof(LightDarkModes))]
public partial class LightDarkModesJsonContext : JsonSerializerContext
{

}