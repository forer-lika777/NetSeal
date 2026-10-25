using System;
using System.Collections.Generic;
using System.Text;

namespace NetSeal.Core.Services.AppSetting;

public interface IKeyItem<T> where T : notnull
{
    public string Name { get; }

    /// <summary>
    /// Get the value of the key item.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<T> GetValueAsync(CancellationToken ct = default);

    /// <summary>
    /// <para>Set the value of the key item.
    /// If the reference of this value was not changed, save operation will not triggered.</para>
    /// <para>To save modified property of the value instance, please use <see cref="SaveAsync(CancellationToken)"/>.</para>
    /// </summary>
    /// <param name="value"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task SetValueAsync(T value, CancellationToken ct = default);

    /// <summary>
    /// Save the reference of the value of the key item.
    /// This is for property-modified situation, as the value reference may not changed.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task SaveAsync(CancellationToken ct = default);
}