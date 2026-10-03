using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetSeal.Services;

public interface IUiDispatcher
{
    bool IsOnUiThread { get; }
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> func);
}