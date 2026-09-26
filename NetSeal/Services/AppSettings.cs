using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage;
using Windows.Storage.Streams;

namespace NetSeal.Services;

public class AppSettings : IAppSettings
{
    public AppSettings()
    {
        
    }

    /// <summary>
    /// 保存是否处于登录状态的键值。
    /// </summary>
    public IKeyItem<bool> HasSavedConnectionAuth { get; } = new KeyItem<bool>(nameof(HasSavedConnectionAuth), false);

    /// <summary>
    /// 保存已选网络名称。
    /// </summary>
    public IKeyItem<string> SelectedConnectionName { get; } = new KeyItem<string>(nameof(SelectedConnectionName), string.Empty);

    /// <summary>
    /// 保存用户名的键值。
    /// </summary>
    public IKeyItem<string> AccountId { get; } = new SecureKeyItem<string>(nameof(AccountId), string.Empty);

    /// <summary>
    /// 保存账户的密码的键值。
    /// </summary>
    public IKeyItem<string> Password { get; } = new SecureKeyItem<string>(nameof(Password), string.Empty);

    /// <summary>
    /// 用于在 LocalSettings 中存储和读取配置项的键值对容器。
    /// 接受直接存储基础类型，也接受支持 JSON 序列化的自定义类型。
    /// 接受的的基础类型：bool, string, int, uint, long, ulong, float, double, char, DateTime, TimeSpan, Guid, byte[].
    /// </summary>
    /// <typeparam name="T">存储的值的类型。</typeparam>
    public class KeyItem<T> : IKeyItem<T> where T : notnull
    {
        /// <summary>
        /// 初始化 KeyItem 的新实例。
        /// </summary>
        /// <param name="name">存储时使用的键名，对应 LocalSettings 中的 Key。</param>
        /// <param name="defaultValue">当设置不存在时返回的默认值。</param>
        /// <param name="jsonSerializerContext">
        /// 用于 JSON 序列化的上下文。对于自定义类型，必须传入对应的 JsonSerializerContext，否则运行时会在保存时抛出异常。
        /// 对于直接支持的类型（int, string, bool, DateTime 等），可以传入 null。
        /// </param>
        public KeyItem(string name, T defaultValue, JsonSerializerContext? jsonSerializerContext = null)
        {
            Name = name;
            value = defaultValue;
            context = jsonSerializerContext;
            Load();
        }

        public string Name { get; }

        /// <summary>
        /// 存储设置项的值。替换该对象时，将自动触发保存。
        /// 如果只是修改属性，必须手动调用 Save() 方法进行保存，否则不会触发保存。
        /// </summary>
        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value))
                    return;
                this.value = value;
                Save();
            }
        }

        private T value;
        private readonly JsonSerializerContext? context;

        public void Load()
        {
            try
            {
                var raw = ApplicationData.Current.LocalSettings.Values[Name];

                if (raw == null)
                {
                    Debug.WriteLine($"设置 '{Name}' 不存在，使用默认值。");
                    return;
                }

                if (isDirectlySupported)
                {
                    value = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
                    return;
                }

                if (context == null)
                    throw new ArgumentNullException(nameof(context), "使用自定义类型时，必须提供 JSON Serialize Context。");

                if (raw is string json)
                {
                    var deserialized = JsonSerializer.Deserialize(json, typeof(T), context);
                    if (deserialized is T typed)
                    {
                        value = typed;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载设置 '{Name}' 失败。错误信息: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                string valueToStore;

                if (isDirectlySupported)
                {
                    valueToStore = (string)Convert.ChangeType(value, typeof(string), CultureInfo.InvariantCulture);
                }
                else
                {
                    if (context == null)
                        throw new ArgumentNullException(nameof(context), $"使用自定义类型{typeof(T).Name}时，必须提供 JSON Serialize Context。");

                    valueToStore = JsonSerializer.Serialize(value, typeof(T), context);
                }

                ApplicationData.Current.LocalSettings.Values[Name] = valueToStore;
                //config[Name] = valueToStore;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"保存设置 '{Name}' 失败。类型：{typeof(T).Name}，序列化错误信息: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存设置 '{Name}' 失败。错误信息: {ex.Message}");
            }
        }

        private static readonly bool isDirectlySupported =
            typeof(T) == typeof(bool) ||
            typeof(T) == typeof(string) ||
            typeof(T) == typeof(int) ||
            typeof(T) == typeof(uint) ||
            typeof(T) == typeof(long) ||
            typeof(T) == typeof(ulong) ||
            typeof(T) == typeof(float) ||
            typeof(T) == typeof(double) ||
            typeof(T) == typeof(char) ||
            typeof(T) == typeof(DateTime) ||
            typeof(T) == typeof(TimeSpan) ||
            typeof(T) == typeof(Guid);
    }

    /// <summary>
    /// 用于在 LocalSettings 中存储和读取配置项的键值对容器，并使用 Windows 平台提供的加密设置进行加密。
    /// 接受直接存储基础类型，也接受支持 JSON 序列化的自定义类型。
    /// 接受的基础类型：string
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SecureKeyItem<T> : IKeyItem<T> where T : notnull
    {
        /// <summary>
        /// 初始化 KeyItem 的新实例。
        /// </summary>
        /// <param name="name">存储时使用的键名，对应 LocalSettings 中的 Key。</param>
        /// <param name="defaultValue">当设置不存在时使用的默认值。</param>
        /// <param name="jsonSerializerContext">
        /// 用于 JSON 序列化的上下文。对于自定义类型，必须传入对应的 JsonSerializerContext，否则运行时会抛出异常。
        /// 对于 string 类型，可以传入 null。
        /// </param>
        public SecureKeyItem(string name, T defaultValue, JsonSerializerContext? jsonSerializerContext = null)
        {
            Name = name;
            value = defaultValue;
            context = jsonSerializerContext;
            Load();
        }

        public string Name { get; }

        /// <summary>
        /// 存储设置项的值。替换该对象时，将自动触发保存。
        /// 如果只是修改属性，必须手动调用 Save() 方法进行保存，否则不会触发保存。
        /// </summary>
        public T Value
        {
            get => value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(this.value, value))
                    return;
                this.value = value;
                Save();
            }
        }

        private T value;
        private readonly JsonSerializerContext? context;

        public void Load()
        {
            try
            {
                var raw = ApplicationData.Current.LocalSettings.Values[Name];
                //var raw = config[Name];

                if (raw is null || raw is not string encryptedData)
                {
                    Save(); // 保存默认值到存储
                    return;
                }

                var decrypted = Decrypt(encryptedData);

                if (typeof(T) == typeof(string))
                {
                    value = (T)Convert.ChangeType(decrypted, typeof(T), CultureInfo.InvariantCulture);
                    return;
                }

                if (context == null)
                {
                    throw new ArgumentNullException($"使用自定义类型 {typeof(T).Name} 时，必须提供 JSON Serialize Context。");
                }

                var deserialized = JsonSerializer.Deserialize(decrypted, typeof(T), context);
                if (deserialized is T typed)
                {
                    value = typed;
                }
                else
                {
                    throw new Exception("反序列化未得到预期类型。");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载设置 '{Name}' 失败。错误信息: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                string jsonData;

                if (typeof(T) == typeof(string))
                {
                    jsonData = (string)Convert.ChangeType(value, typeof(string), CultureInfo.InvariantCulture);
                }
                else
                {
                    if (context == null)
                    {
                        throw new ArgumentNullException($"使用自定义类型 {typeof(T).Name} 时，必须提供 JSON Serialize Context。");
                    }

                    jsonData = JsonSerializer.Serialize(value, typeof(T), context);
                }

                string encrypted = Encrypt(jsonData);
                ApplicationData.Current.LocalSettings.Values[Name] = encrypted;
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"保存设置 '{Name}' 失败。类型：{typeof(T)}，序列化错误信息: {jsonEx.Message}");
                throw new InvalidOperationException($"保存设置 '{Name}' 失败，请检查序列化配置：", jsonEx);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存加密设置失败。设置名: '{Name}'。错误: {ex.Message}");
                throw new InvalidOperationException($"保存设置 '{Name}' 失败。{ex.Message}");
            }
        }

        private static string Encrypt(string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            // "LOCAL=user" 表示该加密数据仅限当前 Windows 登录用户解密
            var provider = new DataProtectionProvider("LOCAL=user");
            IBuffer contentBuffer = CryptographicBuffer.ConvertStringToBinary(data, BinaryStringEncoding.Utf8);

            // 异步转同步获取结果
            IBuffer protectedBuffer = provider.ProtectAsync(contentBuffer).AsTask().GetAwaiter().GetResult();

            // 转为 Base64 字符串保存到 LocalSettings
            return CryptographicBuffer.EncodeToBase64String(protectedBuffer);
        }

        private static string Decrypt(string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            try
            {
                var provider = new DataProtectionProvider();
                IBuffer protectedBuffer = CryptographicBuffer.DecodeFromBase64String(data);

                IBuffer clearBuffer = provider.UnprotectAsync(protectedBuffer).AsTask().GetAwaiter().GetResult();
                return CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, clearBuffer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解密失败，数据可能损坏或密钥不匹配: {ex.Message}");
                throw;
            }
        }
    }
}