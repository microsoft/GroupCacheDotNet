// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CircuitBreakerClient.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.IO;
    using System.Runtime.Serialization;
    using System.Threading;
    using System.Threading.Tasks;

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
        private readonly object @lock = new object();
        private readonly IGroupCacheClient client;
        private readonly TimeSpan backOff;
        private int sequentialFailure;
        private DateTimeOffset lastTry = DateTimeOffset.UtcNow;
        private readonly int maxRetry;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerClient"/> class.
        /// </summary>
        /// <param name="client"></param>
        public CircuitBreakerClient(IGroupCacheClient client)
            : this(client, TimeSpan.FromMinutes(10), 3)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerClient"/> class.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="backOff"></param>
        /// <param name="maxRetry"></param>
        public CircuitBreakerClient(IGroupCacheClient client, TimeSpan backOff, int maxRetry)
        {
            this.client = client;
            this.backOff = backOff;
            this.maxRetry = maxRetry;
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint
        {
            get
            {
                return this.client.Endpoint;
            }
        }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get
            {
                return this.client.IsLocal;
            }
        }

        private bool IsOpen
        {
            get
            {
                return this.sequentialFailure >= this.maxRetry;
            }
        }

        private TimeSpan TimeSinceLastTry
        {
            get
            {
                return DateTimeOffset.Now - this.lastTry;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.client.Dispose();
        }

        /// <inheritdoc/>
        public async Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            try
            {
                this.TripIfNeeded();
                await this.client.GetAsync(group, key, sink, cacheControl, ct).ConfigureAwait(false);
            }
            catch (ServerBusyException)
            {
                // Dont count busy server as bad
                throw;
            }
            catch
            {
                this.CountFailure();
                throw;
            }

            this.ResetCount();
        }

        private void CountFailure()
        {
            lock (this.@lock)
            {
                if (!this.IsOpen)
                {
                    this.sequentialFailure++;
                }
            }
        }

        private void ResetCount()
        {
            lock (this.@lock)
            {
                this.sequentialFailure = 0;
            }
        }

        private void TripIfNeeded()
        {
            lock (this.@lock)
            {
                if (this.IsOpen)
                {
                    if (this.TimeSinceLastTry < this.backOff)
                    {
                        throw new CircuitBreakerOpenException();
                    }
                }

                this.lastTry = DateTimeOffset.UtcNow;
            }
        }
    }

    [Serializable]
    public class CircuitBreakerOpenException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        public CircuitBreakerOpenException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="message"></param>
        public CircuitBreakerOpenException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public CircuitBreakerOpenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircuitBreakerOpenException"/> class.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected CircuitBreakerOpenException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
