﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Features.Authentication;

namespace SystemWebAdapter
{
    public class SystemWebFeatureCollection :
        IFeatureCollection,
        IHttpRequestFeature,
        IHttpResponseFeature,
        IHttpConnectionFeature,
        IHttpRequestIdentifierFeature,
        IHttpRequestLifetimeFeature,
        ITlsConnectionFeature,
        IHttpSendFileFeature,
        IHttpAuthenticationFeature//,
        //IHttpWebSocketFeature,
    {
        private readonly System.Web.HttpContext _httpContext;
        private readonly System.Web.HttpRequest _httpRequest;
        private readonly System.Web.HttpResponse _httpResponse;

        public SystemWebFeatureCollection(System.Web.HttpContext httpContext)
        {
            _httpContext = httpContext;
            _httpRequest = httpContext.Request;
            _httpResponse = httpContext.Response;
        }

        // IHttpRequestFeature
        string IHttpRequestFeature.Protocol
        {
            get { return _httpRequest.ServerVariables["SERVER_PROTOCOL"]; }
            set { }
        }

        string IHttpRequestFeature.Scheme
        {
            get { return _httpRequest.IsSecureConnection ? "https" : "http"; }
            set { }
        }

        string IHttpRequestFeature.Method
        {
            get { return _httpRequest.HttpMethod; }
            set { }
        }

        string IHttpRequestFeature.PathBase
        {
            get { return Utils.NormalizePath(HttpRuntime.AppDomainAppVirtualPath); }
            set { }
        }

        string IHttpRequestFeature.Path
        {
            get { return _httpRequest.AppRelativeCurrentExecutionFilePath.Substring(1) + _httpRequest.PathInfo; }
            set { }
        }

        string IHttpRequestFeature.QueryString
        {
            get
            {
                var requestQueryString = string.Empty;
                var uri = _httpRequest.Url;
                if (uri != null)
                {
                    var query = uri.Query + uri.Fragment; // System.Uri mistakes un-escaped # in the query as a fragment
                    if (query.Length > 1)
                    {
                        // pass along the query string without the leading "?" character
                        requestQueryString = query.Substring(1);
                    }
                }
                return requestQueryString;
            }
            set { }
        }

        private IHeaderDictionary _requestHeaders;
        IHeaderDictionary IHttpRequestFeature.Headers
        {
            get { return _requestHeaders ?? (_requestHeaders = new SystemWebHeaders(_httpRequest.Headers)); }
            set { }
        }
        
        Stream IHttpRequestFeature.Body
        {
            get { return _httpRequest.InputStream; }
            set { }
        }

        // IHttpResponseFeature
        int IHttpResponseFeature.StatusCode
        {
            get { return _httpResponse.StatusCode; }
            set { _httpResponse.StatusCode = value; }
        }

        string IHttpResponseFeature.ReasonPhrase
        {
            get { return _httpResponse.StatusDescription; }
            set { _httpResponse.StatusDescription = value; }
        }

        private IHeaderDictionary _responseHeaders;
        IHeaderDictionary IHttpResponseFeature.Headers
        {
            get { return _responseHeaders ?? (_responseHeaders = new SystemWebHeaders(_httpResponse.Headers)); }
            set { }
        }

        Stream IHttpResponseFeature.Body
        {
            get { return _httpResponse.OutputStream; }
            set { }
        }

        bool IHttpResponseFeature.HasStarted
        {
            get { throw new NotSupportedException("HasStarted isn't yet supported"); }
        }

        void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
        {
            throw new NotSupportedException("OnStarting isn't yet supported");
        }

        void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
        {
            throw new NotSupportedException("OnCompleted isn't yet supported");
        }

        // IHttpConnectionFeature
        IPAddress IHttpConnectionFeature.RemoteIpAddress
        {
            get { return IPAddress.Parse(_httpRequest.ServerVariables["REMOTE_ADDR"]); }
            set { }
        }

        IPAddress IHttpConnectionFeature.LocalIpAddress
        {
            get { return IPAddress.Parse(_httpRequest.ServerVariables["LOCAL_ADDR"]); }
            set { }
        }

        int IHttpConnectionFeature.RemotePort
        {
            get { return int.Parse(_httpRequest.ServerVariables["REMOTE_PORT"]); }
            set { }
        }

        int IHttpConnectionFeature.LocalPort
        {
            get { return int.Parse(_httpRequest.ServerVariables["SERVER_PORT"]); }
            set { }
        }

        bool IHttpConnectionFeature.IsLocal
        {
            get { return _httpRequest.IsLocal; }
            set { }
        }

        // IHttpRequestIdentifierFeature
        string IHttpRequestIdentifierFeature.TraceIdentifier
        {
            get
            {
                var httpWorkerRequest = ((IServiceProvider)_httpContext).GetService(typeof(HttpWorkerRequest)) as HttpWorkerRequest;

                return httpWorkerRequest?.RequestTraceIdentifier.ToString();
            }
            set { }
        }

        // IHttpRequestLifetimeFeature
        void IHttpRequestLifetimeFeature.Abort()
        {
            _httpRequest.Abort();
        }

        CancellationToken IHttpRequestLifetimeFeature.RequestAborted
        {
            get { return _httpResponse.ClientDisconnectedToken; }
            set { }
        }


        // ITlsConnectionFeature
        private X509Certificate2 LoadClientCert
        {
            get
            {
                var cert = (X509Certificate2)null;
                try
                {
                    if (_httpContext.Request.ClientCertificate != null && _httpContext.Request.ClientCertificate.IsPresent)
                    {
                        cert = new X509Certificate2(_httpContext.Request.ClientCertificate.Certificate);
                    }
                }
                catch (CryptographicException)
                {
                }

                return cert;
            }
        }

        Task<X509Certificate2> ITlsConnectionFeature.GetClientCertificateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LoadClientCert);
        }
        
        X509Certificate2 ITlsConnectionFeature.ClientCertificate
        {
            get { return LoadClientCert; }
            set { }
        }
        private bool SupportsClientCerts
        {
            get
            {
                return string.Equals("https", ((IHttpRequestFeature)this).Scheme, StringComparison.OrdinalIgnoreCase);
            }
        }

        // IHttpSendFileFeature
        public Task SendFileAsync(string path, long offset, long? length, CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested)
            {
                return Utils.CancelledTask;
            }

            try
            {
                //OnStart();

                // TransmitFile is not safe to call on a background thread.  It should complete quickly so long as buffering is enabled.
                _httpContext.Response.TransmitFile(path, offset, length ?? -1);

                return Utils.CompletedTask;
            }
            catch (Exception ex)
            {
                return Utils.CreateFaultedTask(ex);
            }
        }


        // IHttpAuthenticationFeature
        private ClaimsPrincipal _requestUser;
        ClaimsPrincipal IHttpAuthenticationFeature.User
        {
            get { return _requestUser ?? (_requestUser = Utils.MakeClaimsPrincipal(_httpContext.User)); }
            set
            {
                _requestUser = null;
                _httpContext.User = value;
            }
        }

        IAuthenticationHandler IHttpAuthenticationFeature.Handler
        {
            get; set;
        }

        // IFeatureCollection
        public int Revision
        {
            get { return 0; } // Not modifiable
        }

        public bool IsReadOnly
        {
            get { return true; }
        }
        
        public object this[Type key]
        {
            get { return Get(key); }
            set { throw new NotSupportedException(); }
        }

        private bool SupportsInterface(Type key)
        {
            // Does this type implement the requested interface?
            if (key.GetTypeInfo().IsAssignableFrom(GetType().GetTypeInfo()))
            {
                // Check for conditional features
                if (key == typeof(ITlsConnectionFeature))
                {
                    return SupportsClientCerts;
                }
                //else if (key == typeof(IHttpWebSocketFeature))
                //{
                //    return SupportsWebSockets;
                //}

                // The rest of the features are always supported.
                return true;
            }
            return false;
        }

        public object Get(Type key)
        {
            if (SupportsInterface(key))
            {
                return this;
            }
            return null;
        }

        public void Set(Type key, object value)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
        {
            yield return new KeyValuePair<Type, object>(typeof(IHttpRequestFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpResponseFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpConnectionFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpRequestIdentifierFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpRequestLifetimeFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpSendFileFeature), this);
            yield return new KeyValuePair<Type, object>(typeof(IHttpAuthenticationFeature), this);

            //// Check for conditional features
            if (SupportsClientCerts)
            {
                yield return new KeyValuePair<Type, object>(typeof(ITlsConnectionFeature), this);
            }
            //if (SupportsWebSockets)
            //{
            //    yield return new KeyValuePair<Type, object>(typeof(IHttpWebSocketFeature), this);
            //}
        }

        public void Dispose()
        {
        }

    }
}
