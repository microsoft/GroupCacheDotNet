// --------------------------------------------------------------------------------------------------------------------
// <copyright file="KestrelServer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using GroupCache;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.Hosting.Server.Features;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Primitives;
    using Microsoft.Net.Http.Headers;

    /// <summary>
    /// Hosts the GroupCache peer protocol on ASP.NET Core Kestrel.
    /// </summary>
    public sealed class KestrelServer : IDisposable, IAsyncDisposable
    {
        public const string GroupNameFormField = "groupName";
        public const string KeyFormField = "key";
        public const string CustomHeadersManifestHeader = "X-GroupCache-Custom-Headers";

        private readonly IGroupCacheClient handler;
        private readonly string host;
        private readonly SemaphoreSlim lifecycleLock = new SemaphoreSlim(1, 1);
        private readonly int port;
        private WebApplication application;
        private bool disposed;
        private bool started;

        public KestrelServer(IGroupCacheClient handler, int port, string host = "127.0.0.1")
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.port = port;
            this.Endpoint = new PeerEndpoint { HostName = host, Port = port };
        }

        public PeerEndpoint Endpoint { get; private set; }

        public void Start()
        {
#pragma warning disable VSTHRD002 // Preserve the synchronous pool constructor while startup itself remains readiness-safe.
            this.StartAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await this.lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(this.disposed, this);
                if (this.started)
                {
                    throw new InvalidOperationException("The Kestrel server has already been started.");
                }

                WebApplication newApplication = null;
                try
                {
                    WebApplicationBuilder builder = WebApplication.CreateBuilder(
                        new WebApplicationOptions { Args = Array.Empty<string>() });
                    builder.Logging.ClearProviders();
                    builder.WebHost.UseKestrel().UseUrls(this.CreateListenUrl());

                    newApplication = builder.Build();
                    newApplication.MapPost("/", this.HandleRequestAsync);
                    await newApplication.StartAsync(cancellationToken).ConfigureAwait(false);

                    this.application = newApplication;
                    this.Endpoint = new PeerEndpoint
                    {
                        HostName = this.host,
                        Port = ResolveBoundPort(newApplication),
                    };
                    this.started = true;
                }
                catch
                {
                    if (newApplication != null)
                    {
                        await newApplication.DisposeAsync().ConfigureAwait(false);
                    }

                    throw;
                }
            }
            finally
            {
                this.lifecycleLock.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
#pragma warning disable VSTHRD002 // IDisposable must bridge to the async Kestrel shutdown path.
            this.DisposeAsync().AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            await this.lifecycleLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                WebApplication currentApplication = this.application;
                this.application = null;
                if (currentApplication != null)
                {
                    await currentApplication.StopAsync().ConfigureAwait(false);
                    await currentApplication.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                this.lifecycleLock.Release();
            }
        }

        private static int ResolveBoundPort(WebApplication application)
        {
            IServer server = application.Services.GetRequiredService<IServer>();
            IServerAddressesFeature addressesFeature = server.Features.Get<IServerAddressesFeature>();
            string address = addressesFeature?.Addresses.SingleOrDefault();
            if (address == null || !Uri.TryCreate(address, UriKind.Absolute, out Uri boundAddress))
            {
                throw new InvalidOperationException("Kestrel did not report its bound address.");
            }

            return boundAddress.Port;
        }

        private static async Task WriteErrorAsync(HttpContext context, int statusCode, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(exception.Message, context.RequestAborted).ConfigureAwait(false);
        }

        private string CreateListenUrl()
        {
            string bindingHost = this.host;
            if (bindingHost == "*" || bindingHost == "+")
            {
                bindingHost = "0.0.0.0";
            }
            else if (this.port == 0 && string.Equals(bindingHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Kestrel requires an explicit loopback address when the operating system selects the port.
                bindingHost = "127.0.0.1";
            }

            return new UriBuilder(Uri.UriSchemeHttp, bindingHost, this.port).Uri.AbsoluteUri;
        }

        private async Task HandleRequestAsync(HttpContext context)
        {
            if (!context.Request.HasFormContentType)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            string groupName = form[GroupNameFormField].FirstOrDefault();
            string key = form[KeyFormField].FirstOrDefault();
            if (groupName == null || key == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            context.Response.ContentType = "application/octet-stream";
            try
            {
                var cacheControl = new HttpResponseCacheControl(context.Response.Headers);
                await this.handler
                    .GetAsync(groupName, key, context.Response.Body, cacheControl, context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (GroupNotFoundException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, exception).ConfigureAwait(false);
            }
            catch (ServerBusyException exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status503ServiceUnavailable, exception).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                context.Abort();
            }
            catch (Exception exception)
            {
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, exception).ConfigureAwait(false);
            }
        }

        private sealed class HttpResponseCacheControl : ICacheControl
        {
            private const string NoStoreDirective = "no-store";
            private readonly IHeaderDictionary headers;

            public HttpResponseCacheControl(IHeaderDictionary headers)
            {
                this.headers = headers;
                this.CustomHeaders = new StringHeaderDictionary(headers);
            }

            public bool NoStore
            {
                get
                {
                    return this.GetCacheControlDirectives().Any(
                        directive => string.Equals(directive, NoStoreDirective, StringComparison.OrdinalIgnoreCase));
                }

                set
                {
                    List<string> directives = this.GetCacheControlDirectives()
                        .Where(directive => !string.Equals(
                            directive,
                            NoStoreDirective,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (value)
                    {
                        directives.Add(NoStoreDirective);
                    }

                    if (directives.Count == 0)
                    {
                        this.headers.Remove(HeaderNames.CacheControl);
                    }
                    else
                    {
                        this.headers[HeaderNames.CacheControl] = string.Join(", ", directives);
                    }
                }
            }

            public IDictionary<string, string> CustomHeaders { get; }

            private IEnumerable<string> GetCacheControlDirectives()
            {
                return this.headers[HeaderNames.CacheControl]
                    .SelectMany(value => value.Split(','))
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0);
            }
        }

        private sealed class StringHeaderDictionary : IDictionary<string, string>
        {
            private readonly HashSet<string> customHeaderNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            private readonly IHeaderDictionary headers;

            public StringHeaderDictionary(IHeaderDictionary headers)
            {
                this.headers = headers;
            }

            public ICollection<string> Keys => this.customHeaderNames.ToList();

            public ICollection<string> Values => this.customHeaderNames
                .Select(name => this.headers[name].ToString())
                .ToList();

            public int Count => this.customHeaderNames.Count;

            public bool IsReadOnly => false;

            public string this[string key]
            {
                get
                {
                    if (!this.customHeaderNames.Contains(key))
                    {
                        throw new KeyNotFoundException($"No custom header named '{key}' exists.");
                    }

                    return this.headers[key].ToString();
                }

                set
                {
                    EnsureNotManifestHeader(key);
                    this.customHeaderNames.Add(key);
                    this.headers[key] = value;
                    this.UpdateManifest();
                }
            }

            public void Add(string key, string value)
            {
                EnsureNotManifestHeader(key);
                if (this.customHeaderNames.Contains(key))
                {
                    throw new ArgumentException($"A header named '{key}' already exists.", nameof(key));
                }

                this.customHeaderNames.Add(key);
                this.headers[key] = new StringValues(value);
                this.UpdateManifest();
            }

            public void Add(KeyValuePair<string, string> item)
            {
                this.Add(item.Key, item.Value);
            }

            public void Clear()
            {
                foreach (string headerName in this.customHeaderNames)
                {
                    this.headers.Remove(headerName);
                }

                this.customHeaderNames.Clear();
                this.UpdateManifest();
            }

            public bool Contains(KeyValuePair<string, string> item)
            {
                return this.TryGetValue(item.Key, out string value)
                    && string.Equals(value, item.Value, StringComparison.Ordinal);
            }

            public bool ContainsKey(string key)
            {
                return this.customHeaderNames.Contains(key);
            }

            public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
            {
                foreach (KeyValuePair<string, string> item in this)
                {
                    array[arrayIndex++] = item;
                }
            }

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                foreach (KeyValuePair<string, StringValues> header in this.headers)
                {
                    if (this.customHeaderNames.Contains(header.Key))
                    {
                        yield return new KeyValuePair<string, string>(header.Key, header.Value.ToString());
                    }
                }
            }

            public bool Remove(string key)
            {
                if (!this.customHeaderNames.Remove(key))
                {
                    return false;
                }

                this.headers.Remove(key);
                this.UpdateManifest();
                return true;
            }

            public bool Remove(KeyValuePair<string, string> item)
            {
                return this.Contains(item) && this.Remove(item.Key);
            }

            public bool TryGetValue(string key, out string value)
            {
                if (this.customHeaderNames.Contains(key)
                    && this.headers.TryGetValue(key, out StringValues values))
                {
                    value = values.ToString();
                    return true;
                }

                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            private static void EnsureNotManifestHeader(string key)
            {
                if (string.Equals(key, CustomHeadersManifestHeader, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        $"'{CustomHeadersManifestHeader}' is reserved by the GroupCache peer protocol.",
                        nameof(key));
                }
            }

            private void UpdateManifest()
            {
                if (this.customHeaderNames.Count == 0)
                {
                    this.headers.Remove(CustomHeadersManifestHeader);
                }
                else
                {
                    this.headers[CustomHeadersManifestHeader] = string.Join(",", this.customHeaderNames);
                }
            }
        }
    }
}
