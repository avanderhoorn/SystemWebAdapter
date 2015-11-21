using System;
using System.Threading;
using System.Web;
using System.Web.Compilation;

namespace SystemWebAdapter.Sample.Framework
{
    public class AgentHttpModule : IHttpModule
    {
        public void Init(HttpApplication httpApplication)
        {
            // TODO: Deal with this getting called multiple times per request
            httpApplication.BeginRequest += (context, e) => BeginRequest(GetHttpContext(context));
            httpApplication.PostReleaseRequestState += (context, e) => EndRequest(GetHttpContext(context));
        }

        internal void BeginRequest(Microsoft.AspNet.Http.HttpContext httpContext)
        {
            // SET BREAKPOINT HERE
        }

        internal void EndRequest(Microsoft.AspNet.Http.HttpContext httpContext)
        {
        }

        private static Microsoft.AspNet.Http.HttpContext GetHttpContext(object sender)
        {
            var httpApplication = sender as HttpApplication;

            return httpApplication.Context.CreateContext();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}