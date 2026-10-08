using OldSchoolForms.Server.Runtime.Scripting;

namespace OldSchoolForms.Server.Runtime.Services;

/// <summary>
/// Manages one <see cref="SemaphoreSlim"/> per service ID. Jint engines are
/// not thread-safe and CoreRemoting delivers calls on worker threads, so
/// every invocation of a service is serialized through its semaphore.
/// </summary>
public sealed class ServiceDispatcher : IDisposable
{
    private readonly Dictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs an action while holding the service's lock.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="serviceId">The service ID whose lock to acquire.</param>
    /// <param name="action">The action to run exclusively.</param>
    /// <returns>The action's result.</returns>
    public T Run<T>(string serviceId, Func<T> action)
    {
        var semaphore = GetOrAdd(serviceId);
        semaphore.Wait();
        try
        {
            return action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Runs an async action while holding the service's lock.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="serviceId">The service ID whose lock to acquire.</param>
    /// <param name="action">The async action to run exclusively.</param>
    /// <returns>A task with the action's result.</returns>
    public async Task<T> RunAsync<T>(string serviceId, Func<Task<T>> action)
    {
        var semaphore = GetOrAdd(serviceId);
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await action().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private SemaphoreSlim GetOrAdd(string serviceId)
    {
        lock (_locks)
        {
            if (!_locks.TryGetValue(serviceId, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _locks[serviceId] = semaphore;
            }

            return semaphore;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_locks)
        {
            foreach (var semaphore in _locks.Values)
                semaphore.Dispose();
            _locks.Clear();
        }
    }
}
