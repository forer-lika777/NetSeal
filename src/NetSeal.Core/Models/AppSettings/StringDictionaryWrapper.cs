using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSeal.Core.Models.AppSettings;

// 1. 定义你的包装类
public class StringDictionaryWrapper
{
    public Dictionary<string, string> Data { get; set; } = new();
}

// 2. 在你原有的 JsonSerializerContext 里加上它
[JsonSerializable(typeof(StringDictionaryWrapper))]
public partial class StringDictionaryJsonContext : JsonSerializerContext
{ }