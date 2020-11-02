using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public interface ICacheControl
    {
        //Whether cache must not store any response.
        bool NoStore { get; set; }
    }

    public class CacheControl : ICacheControl
    {
        //Whether cache must not store any response.
        public bool NoStore { get; set; } = false;
    }

    /// <summary>
    /// A Getter loads data for identified by key.
    /// 
    /// The returned data must be unversioned.
    /// That is, key must uniquely describe the loaded data
    /// without an implicit current time, and without relying on cache expiration mechanisms.
    /// </summary>
    public interface IGetter
    {
        Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct);
    }

    public sealed class GetterFunc : IGetter
    {
        private Func<string, Stream, ICacheControl, CancellationToken, Task> _func;
        public GetterFunc(Func<string, Stream, ICacheControl, CancellationToken, Task> func)
        {
            _func = func;
        }
        public Task GetAsync(string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            return _func(key, sink, cacheControl, ct);
        }
    }

}
