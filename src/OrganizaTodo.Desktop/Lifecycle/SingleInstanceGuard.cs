namespace OrganizaTodo.Desktop.Lifecycle;

public sealed class SingleInstanceGuard(string name) : IDisposable
{
    private Mutex? _mutex;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return createdNew;
    }

    public void Dispose() => _mutex?.Dispose();
}
