using System;
using System.Threading.Tasks;

namespace NetSeal.WinUI.Services;

public interface IUiDispatcher
{
    bool IsOnUiThread { get; }
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> func);
}