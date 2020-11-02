
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    /// <summary>
    /// Usually when some server is slow/down/overloaded, we want to simply send request to a failover destination.
    /// Once server is marked bad, failover becomes instant, we don't need to do 2 round trips 
    /// (one to known slow/broken box, one to failover destination) for a request.
    /// 
    /// The CircuitBreaker will open after maxRetry request fail in a row.
    /// What happens after a client CircuitBreaker is open: 
    ///     1- Every request sent to the Circuit breaker is immediately discarded with CircuitBreakerOpenException.
    ///     2- After backOff time have elapsed since last failed request circuit breaker will try sending one request.
    ///      a- if that request succeed the Circuit Breaker is closed and let all request go through.
    /// </summary>
    public sealed class CircuitBreakerClient : IGroupCacheClient
    {
        private Object _lock = new Object();
        private IGroupCacheClient _client;
        private readonly TimeSpan _backOff;
        private int _sequentialFailure;
        private DateTimeOffset _lastTry = DateTimeOffset.UtcNow;
        private readonly int _maxRetry;

        public CircuitBreakerClient(IGroupCacheClient client) : this(client, TimeSpan.FromMinutes(10), 3)
        {

        }

        public CircuitBreakerClient(IGroupCacheClient client, TimeSpan backOff, int maxRetry)
        {
            this._client = client;
            this._backOff = backOff;
            this._maxRetry = maxRetry;
        }

        public PeerEndpoint Endpoint
        {
            get
            {
                return _client.Endpoint;
            }
        }

        public bool IsLocal
        {
            get
            {
                return _client.IsLocal;
            }
        }

        private bool IsOpen
        {
            get
            {
                return _sequentialFailure >= _maxRetry;
            }
        }

        private TimeSpan TimeSinceLastTry
        {
            get
            {
                return DateTimeOffset.Now - _lastTry;
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        public async Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            try
            {
                TripIfNeeded();
                await _client.GetAsync(group, key, sink, cacheControl, ct).ConfigureAwait(false);
            }
            catch (ServerBusyException)
            {
                // Dont count busy server as bad
                throw;
            }
            catch
            {
                CountFailure();
                throw;
            }
            ResetCount();
        }

        private void CountFailure()
        {
            lock (_lock)
            {
                if (!IsOpen)
                {
                    _sequentialFailure++;
                }
            }
        }

        private void ResetCount()
        {
            lock (_lock)
            {
                _sequentialFailure = 0;
            }
        }

        private void TripIfNeeded()
        {
            lock (_lock)
            {
                if (IsOpen)
                {
                    if (TimeSinceLastTry < _backOff)
                    {
                        throw new CircuitBreakerOpenException();
                    }
                }
                _lastTry = DateTimeOffset.UtcNow;
            }

        }
    }

    [Serializable]
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException()
        {
        }

        public CircuitBreakerOpenException(string message) : base(message)
        {
        }

        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CircuitBreakerOpenException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
