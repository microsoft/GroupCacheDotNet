// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinClient.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;

    public class OwinClient : IGroupCacheClient, IDisposable
    {
        private readonly HttpClient client;

        /// <inheritdoc/>
        public PeerEndpoint Endpoint { get; private set; }

        /// <inheritdoc/>
        public bool IsLocal
        {
            get { return false; }
        }

        public OwinClient(PeerEndpoint endpoint, int minuteTimeout = 2)
        {
            this.Endpoint = endpoint;
            this.client = new HttpClient()
            {
                Timeout = TimeSpan.FromMinutes(minuteTimeout),
                BaseAddress = new UriBuilder("http", this.Endpoint.HostName, this.Endpoint.Port).Uri,
            };
            this.client.DefaultRequestHeaders.ConnectionClose = true;
        }

        /// <inheritdoc/>
        public async Task GetAsync(string group, string key, Stream sink, ICacheControl cacheControl, CancellationToken ct)
        {
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>(OwinServer.GROUPNAME, group),
                new KeyValuePair<string, string>(OwinServer.KEY, key),
            });

            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, "Get")
            {
                Content = formContent,
                Version = HttpVersion.Version10,
            };

            // GetAsync block until the response’s (headers + content) is fully downloaded/read to the memory
            // Unless we use HttpCompletionOption.ResponseHeadersRead
            HttpResponseMessage response;
            try
            {
                response = await this.client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new ConnectFailureException($"fail to send request to {this.Endpoint.ToString()}", ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        throw new InternalServerErrorException(response.ReasonPhrase);
                    }

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        throw new ServerBusyException(response.ReasonPhrase);
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new GroupNotFoundException(response.ReasonPhrase);
                    }

                    response.EnsureSuccessStatusCode();
                }

                // if we reach her we can safely read response.Content
                if (response.Content == null)
                {
                    throw new InternalServerErrorException("response body is null");
                }

                if (response.Headers.CacheControl != null && response.Headers.CacheControl.NoStore)
                {
                    cacheControl.NoStore = true;
                }

                // using ct.Register to force CopyToAsync to return early and throw OperationCanceledException
                using (ct.Register(response.Dispose))
                {
                    try
                    {
                        await response.Content.CopyToAsync(sink).ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException e) when (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(null, e, ct);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.client.Dispose();
        }
    }
}
