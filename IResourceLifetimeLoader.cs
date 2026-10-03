using System.Threading;
using System.Threading.Tasks;

namespace ResourceLoader
{
/// <summary>Optional lifetime API. Existing IResourceLoader implementations remain source compatible.</summary>
public interface IResourceLifetimeLoader : IResourceLoader
{
    Task<T> LoadResourceForLifetimeAsync<T>(string resourceId, ResourceLifetime lifetime,
        CancellationToken token = default);

    Task<IResourceLease<T>> LoadResourceLeaseForLifetimeAsync<T>(string resourceId, ResourceLifetime lifetime,
        CancellationToken token = default);

    Task<IInstanceLease<T>> LoadInstanceLeaseForLifetimeAsync<T, TParent>(string resourceId, TParent parent,
        ResourceLifetime lifetime, CancellationToken token = default) where T : class;
}
}
