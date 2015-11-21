using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SystemWebAdapter
{
    public static class SystemWebExtensions
    {
        public static Microsoft.AspNet.Http.HttpContext CreateContext(this System.Web.HttpContext systemWebHttpContext)
        {
            return new Microsoft.AspNet.Http.Internal.DefaultHttpContext(new Microsoft.AspNet.Http.Features.FeatureCollection(new SystemWebFeatureCollection(systemWebHttpContext)));
        }
    }
}
