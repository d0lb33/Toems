﻿using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;

namespace Toems_ApplicationApi
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            config.Formatters.XmlFormatter.SupportedMediaTypes.Clear();
            // Web API routes
            config.MapHttpAttributeRoutes();

            config.EnableCors();

            config.Routes.MapHttpRoute("DefaultApi", "{controller}/{action}/{id}", new {id = RouteParameter.Optional}
                );

            var json = config.Formatters.JsonFormatter;
            json.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Unspecified;
        }
    }
}