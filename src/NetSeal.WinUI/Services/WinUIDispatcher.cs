using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace NetSeal.Services;

public class WinUIDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue queue;
    public WinUIDispatcher(DispatcherQueue queue)
    {
        this.queue = queue;
    }

    public bool IsOnUiThread => queue.HasThreadAccess;

    public Task InvokeAsync(Action action)
    {
        if (IsOnUiThread)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        queue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (IsOnUiThread)
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>();
        queue.TryEnqueue(() =>
        {
            try
            {
                tcs.SetResult(func());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}