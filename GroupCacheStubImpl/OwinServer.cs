using GroupCache;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Linq;

namespace GroupCacheStub
{
    public class OwinServer : IDisposable
    {
        public const string GROUPNAME = "groupName";
        public const string KEY = "key";
        private const string BASEURL = "/";
        private IDisposable _server;
        private IGroupCacheClient _handler;
        private string _host;

        // Admin rights are not needed for port values of 5000 and higher
        private int _port;
        /// <summary>
        /// Creates a new server.
        /// </summary>
        /// <param name="host">the host</param>
        /// <param name="port">the port. If zero, an unused port is chosen automatically.</param>
        public OwinServer(IGroupCacheClient handler, int port, string host = "*")
        {
            _host = host;
            _port = port;
            _handler = handler;
        }

        public void Start()
        {
            var baseUrl = new UriBuilder("http", _host, _port).ToString();
            var startOption = new StartOptions(baseUrl) { ServerFactory = "Microsoft.Owin.Host.HttpListener" };
            // WebApp class is from  Microsoft.Owin.Hosting
            _server = WebApp.Start(startOption, Configuration);
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server.Dispose();
            }
        }

        public void Configuration(IAppBuilder app)
        {
            app.Run(async context =>
            {
                var request = context.Request;
                var response = context.Response;
                var CallCancelled = context.Request.CallCancelled;


                var form = await request.ReadFormAsync();
                var groupName = form.Get(GROUPNAME);
                var key = form.Get(KEY);

                if (groupName == null || key == null)
                {
                    response.StatusCode = 400; //BadRequest;
                    return;
                }

                response.ContentType = "application/octet-stream";
                try
                {
                    var cacheControl = new OwinCacheControl(context.Response.Headers);
                    await _handler.GetAsync(groupName, key, response.Body, cacheControl, CallCancelled);
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
        const string cacheControlHeaderName = "Cache-Control";
        const string NoStoreHeaderValue = "no-store";
        private IHeaderDictionary _headers;
        public OwinCacheControl(IHeaderDictionary headers)
        {
            _headers = headers;
        }

        public bool NoStore
        {
            get
            {
                var values = _headers.GetCommaSeparatedValues(cacheControlHeaderName);
                return values != null && values.Contains(NoStoreHeaderValue);
            }
            set
            {
                if (value == true)
                {
                    _headers.AppendCommaSeparatedValues(cacheControlHeaderName, new string[] { NoStoreHeaderValue });
                }
                else
                {
                    var values = _headers.GetCommaSeparatedValues(cacheControlHeaderName);
                    if (values != null)
                    {
                        var valuesMinusNoStore = values.Where((val) => val != NoStoreHeaderValue).ToArray();
                        _headers.SetCommaSeparatedValues(cacheControlHeaderName, valuesMinusNoStore);
                    }

                }
            }
        }
    }
}
