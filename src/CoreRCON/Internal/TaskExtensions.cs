namespace CoreRCON.Internal;

internal static class TaskExtensions
{
    public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan? timeout)
    {
        timeout ??= TimeSpan.Zero;
        if (timeout == TimeSpan.Zero || (task.Status is TaskStatus.Canceled or TaskStatus.RanToCompletion or TaskStatus.Faulted))
        {
            return await task;
        }

        using var cancellation = new CancellationTokenSource();
        if (await Task.WhenAny(task, Task.Delay(timeout.Value, cancellation.Token)).ConfigureAwait(false) == task)
        {
            cancellation.Cancel();
            return await task;
        }

        throw new TimeoutException($"An asynchronous operation exceeded the configured timeout of '{timeout.Value}'.");
    }
}
