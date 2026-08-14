// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GroupCacheHttpClient.cs" company="Microsoft Corporation">
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

    /// <summary>
    /// Calls a remote GroupCache endpoint over HTTP.
    /// </summary>
    public sealed class GroupCacheHttpClient : IGroupCacheClient
    {
        private readonly HttpClient client;

        public GroupCacheHttpClient(PeerEndpoint endpoint, int minuteTimeout = 2)
        {
            this.Endpoint = endpoint;
            this.client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(minuteTimeout),
                BaseAddress = new UriBuilder(Uri.UriSchemeHttp, endpoint.HostName, endpoint.Port).Uri,
            };
        }

        /// <inheritdoc/>
        public PeerEndpoint Endpoint { get; }

        /// <inheritdoc/>
        public bool IsLocal => false;

        /// <inheritdoc/>
        public async Task GetAsync(
            string group,
            string key,
            Stream sink,
            ICacheControl cacheControl,
            CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/")
            {
                Content = new FormUrlEncodedContent(
                    new[]
                    {
                        new KeyValuePair<string, string>(KestrelServer.GroupNameFormField, group),
                        new KeyValuePair<string, string>(KestrelServer.KeyFormField, key),
                    }),
            };

            HttpResponseMessage response;
            try
            {
                response = await this.client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                throw new ConnectFailureException($"Failed to send request to {this.Endpoint}.", exception);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    await ThrowResponseExceptionAsync(response, ct).ConfigureAwait(false);
                }

                cacheControl.NoStore = response.Headers.CacheControl?.NoStore == true;
                CopyCustomHeaders(response, cacheControl);

                await response.Content.CopyToAsync(sink, ct).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.client.Dispose();
        }

        private static void CopyCustomHeaders(HttpResponseMessage response, ICacheControl cacheControl)
        {
            cacheControl.CustomHeaders.Clear();
            if (!response.Headers.TryGetValues(
                KestrelServer.CustomHeadersManifestHeader,
                out IEnumerable<string> manifestValues))
            {
                return;
            }

            foreach (string manifestValue in manifestValues)
            {
                foreach (string headerName in manifestValue.Split(','))
                {
                    string trimmedHeaderName = headerName.Trim();
                    if (trimmedHeaderName.Length == 0)
                    {
                        continue;
                    }

                    if (response.Headers.TryGetValues(trimmedHeaderName, out IEnumerable<string> values)
                        || response.Content.Headers.TryGetValues(trimmedHeaderName, out values))
                    {
                        cacheControl.CustomHeaders[trimmedHeaderName] = string.Join(", ", values);
                    }
                }
            }
        }

        private static async Task ThrowResponseExceptionAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            string message = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}";
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    throw new GroupNotFoundException(message);
                case HttpStatusCode.ServiceUnavailable:
                    throw new ServerBusyException(message);
                case HttpStatusCode.InternalServerError:
                    throw new InternalServerErrorException(message);
                default:
                    throw new InternalServerErrorException(
                        $"Unexpected HTTP status {(int)response.StatusCode}: {message}");
            }
        }
    }
}
