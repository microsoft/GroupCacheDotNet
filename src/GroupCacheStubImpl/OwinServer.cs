// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OwinServer.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCacheStub
{
    using System;
    using System.Linq;
    using GroupCache;
    using Microsoft.Owin;
    using Microsoft.Owin.Hosting;
    using Owin;

    public class OwinServer : IDisposable
    {
        public const string GROUPNAME = "groupName";
        public const string KEY = "key";
        private const string BASEURL = "/";
        private IDisposable server;
        private readonly IGroupCacheClient handler;
        private readonly string host;

        // Admin rights are not needed for port values of 5000 and higher
        private readonly int port;

        /// <summary>
        /// Initializes a new instance of the <see cref="OwinServer"/> class.
        /// Creates a new server.
        /// </summary>
        /// <param name="host">the host.</param>
        /// <param name="port">the port. If zero, an unused port is chosen automatically.</param>
        public OwinServer(IGroupCacheClient handler, int port, string host = "*")
        {
            this.host = host;
            this.port = port;
            this.handler = handler;
        }

        public void Start()
        {
            var baseUrl = new UriBuilder("http", this.host, this.port).ToString();
            var startOption = new StartOptions(baseUrl) { ServerFactory = "Microsoft.Owin.Host.HttpListener" };

            // WebApp class is from  Microsoft.Owin.Hosting
            this.server = WebApp.Start(startOption, this.Configuration);
        }

        public void Dispose()
        {
            if (this.server != null)
            {
                this.server.Dispose();
            }
        }

        public void Configuration(IAppBuilder app)
        {
            app.Run(async context =>
            {
                var request = context.Request;
                var response = context.Response;
                var callCancelled = context.Request.CallCancelled;

                var form = await request.ReadFormAsync();
                var groupName = form.Get(GROUPNAME);
                var key = form.Get(KEY);

                if (groupName == null || key == null)
                {
                    response.StatusCode = 400; // BadRequest;
                    return;
                }

                response.ContentType = "application/octet-stream";
                try
                {
                    var cacheControl = new OwinCacheControl(context.Response.Headers);
                    await this.handler.GetAsync(groupName, key, response.Body, cacheControl, callCancelled);
                }
                catch (GroupNotFoundException ex)
                {
                    response.StatusCode = 404;
                    response.ReasonPhrase = ex.ToString();
                }
                catch (ServerBusyException busy)
                {
                    response.StatusCode = 503;
                    response.ReasonPhrase = busy.ToString();
                }
                catch (Exception ex)
                {
                    response.StatusCode = 500;
                    response.ReasonPhrase = ex.ToString();
                }
            });
        }
    }

    public class OwinCacheControl : ICacheControl
    {
        private const string CacheControlHeaderName = "Cache-Control";
        private const string NoStoreHeaderValue = "no-store";
        private readonly IHeaderDictionary headers;

        public OwinCacheControl(IHeaderDictionary headers)
        {
            this.headers = headers;
        }

        public bool NoStore
        {
            get
            {
                var values = this.headers.GetCommaSeparatedValues(CacheControlHeaderName);
                return values != null && values.Contains(NoStoreHeaderValue);
            }

            set
            {
                if (value == true)
                {
                    this.headers.AppendCommaSeparatedValues(CacheControlHeaderName, new string[] { NoStoreHeaderValue });
                }
                else
                {
                    var values = this.headers.GetCommaSeparatedValues(CacheControlHeaderName);
                    if (values != null)
                    {
                        var valuesMinusNoStore = values.Where((val) => val != NoStoreHeaderValue).ToArray();
                        this.headers.SetCommaSeparatedValues(CacheControlHeaderName, valuesMinusNoStore);
                    }
                }
            }
        }
    }
}
