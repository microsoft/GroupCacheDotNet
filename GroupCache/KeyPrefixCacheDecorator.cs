using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public class KeyPrefixCacheDecorator : ICache
    {
        private ICache _decorated;
        private string _prefix;
        public KeyPrefixCacheDecorator(string prefix, ICache cache)
        {
            _prefix = prefix;
            _decorated = cache;
            _decorated.ItemOverCapacity += (key) => ItemOverCapacity?.Invoke(key);
        }

        public event Action<string> ItemOverCapacity;

        public Task<ICacheEntry> GetOrAddAsync(string key, Func<string, Stream, ICacheControl, Task> valueFactory, ICacheControl cacheControl, CancellationToken ct)
        {
            var newkey = _prefix + key;
            return _decorated.GetOrAddAsync(newkey, (str, stream, cc) =>  valueFactory(key, stream, cc), cacheControl, ct);
        }

        public Task RemoveAsync(string key, CancellationToken ct)
        {
            var newkey = _prefix + key;
            return _decorated.RemoveAsync(newkey, ct);
        }
    }
}
