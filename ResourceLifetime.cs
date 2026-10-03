using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ResourceLoader
{
/// <summary>Owns resource leases for one consumer. Create one lifetime per owner, not per load.</summary>
public sealed class ResourceLifetime : IDisposable
{
    private readonly object _gate = new object();
    private readonly HashSet<IDisposable> _leases = new HashSet<IDisposable>();
    private readonly CancellationTokenSource _closed = new CancellationTokenSource();
    private bool _disposed;

    public bool IsDisposed
    {
        get { lock (_gate) return _disposed; }
    }

    internal CancellationToken ClosedToken => _closed.Token;

    /// <summary>Binds a lifetime to a GameObject. Destroying the object closes the lifetime.</summary>
    public static ResourceLifetime For(GameObject owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var anchor = owner.GetComponent<ResourceLifetimeAnchor>();
        if (anchor == null) anchor = owner.AddComponent<ResourceLifetimeAnchor>();
        return anchor.Lifetime;
    }

    public IResourceLease<T> Add<T>(IResourceLease<T> lease)
    {
        if (lease == null) throw new ArgumentNullException(nameof(lease));
        return Register(new BoundResourceLease<T>(this, lease));
    }

    public IInstanceLease<T> Add<T>(IInstanceLease<T> lease)
    {
        if (lease == null) throw new ArgumentNullException(nameof(lease));
        return Register(new BoundInstanceLease<T>(this, lease));
    }

    private T Register<T>(T lease) where T : IDisposable
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _leases.Add(lease);
                return lease;
            }
        }
        lease.Dispose();
        throw new ObjectDisposedException(nameof(ResourceLifetime));
    }

    private void Remove(IDisposable lease)
    {
        lock (_gate) _leases.Remove(lease);
    }

    public void Dispose()
    {
        IDisposable[] leases;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            leases = new IDisposable[_leases.Count];
            _leases.CopyTo(leases);
            _leases.Clear();
        }
        _closed.Cancel();
        Exception firstFailure = null;
        foreach (var lease in leases)
        {
            try { lease.Dispose(); }
            catch (Exception ex) { if (firstFailure == null) firstFailure = ex; }
        }
        // Retain the CTS: a cancelled token may still be observed by in-flight callers.
        if (firstFailure != null) throw firstFailure;
    }

    private sealed class BoundResourceLease<T> : IResourceLease<T>
    {
        private readonly ResourceLifetime _owner;
        private readonly IResourceLease<T> _inner;
        private int _disposed;
        public T Resource => _inner.Resource;
        public BoundResourceLease(ResourceLifetime owner, IResourceLease<T> inner) { _owner = owner; _inner = inner; }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner.Remove(this);
            _inner.Dispose();
        }
    }

    private sealed class BoundInstanceLease<T> : IInstanceLease<T>
    {
        private readonly ResourceLifetime _owner;
        private readonly IInstanceLease<T> _inner;
        private int _disposed;
        public T Instance => _inner.Instance;
        public BoundInstanceLease(ResourceLifetime owner, IInstanceLease<T> inner) { _owner = owner; _inner = inner; }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _owner.Remove(this);
            _inner.Dispose();
        }
    }
}

}
