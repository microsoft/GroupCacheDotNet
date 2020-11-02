using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public interface ICache
    {
        Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct);
        event Action<string> ItemOverCapacity;

        Task RemoveAsync(string key, CancellationToken ct);
    }

    public interface ICacheEntry
    {
        Stream Value();
        void Ref();
        Task DisposeAsync();
    }

    public class EmptyCacheEntry : ICacheEntry
    {
        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public void Ref() {}
        public Stream Value()
        {
            throw new NotImplementedException("Empty entry have no stream");
        }
    }
}
