using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs
{
    public interface IStore<TKey, TValue>
        where TKey : notnull
        where TValue : class
    {
        IEnumerable<TKey> Keys { get; }
        IEnumerable<TValue> Values { get; }

        Task<bool> ExistsAsync(TKey name, CancellationToken cancel = default);
        Task<TValue> GetAsync(TKey name, CancellationToken cancel = default);
        Task PutAsync(TKey name, TValue value, CancellationToken cancel = default);
        Task RemoveAsync(TKey name, CancellationToken cancel = default);
        Task<ulong?> SizeOfAsync(TKey name, CancellationToken cancel = default);
        Task<TValue?> TryGetAsync(TKey name, CancellationToken cancel = default);
        void SetNamespace(string? ns);
    }
}
