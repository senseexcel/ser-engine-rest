#region License
/*
Copyright (c) 2019 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;
    using Ser.Engine.Rest.Filters;
    using Microsoft.OpenApi.Models;
    using System.Collections.Generic;
    using NLog;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.Extensions.Hosting;
    using Ser.Engine.Rest.Services;
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Rewrite;
    using System.Net;
    using Prometheus;
    using System.Text;
    #endregion

    /// <summary>
    /// Startup class of the service
    /// </summary>
    public class Startup
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties And Variables
        private readonly IHostingEnvironment HostingEnv = null;
        private readonly IConfiguration Configuration = null;
        #endregion

        #region Constructor
        /// <summary>
        /// Startup Constructor
        /// </summary>
        /// <param name="env">Hosting Varibales</param>
        /// <param name="configuration">App Configuration</param>
        public Startup(IHostingEnvironment env, IConfiguration configuration)
        {
            HostingEnv = env;
            Configuration = configuration;
        }
        #endregion

        #region Configuration part
        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services">Service Parameter</param>
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                var tempFolder = Path.Combine(AppContext.BaseDirectory, Configuration.GetValue<string>("contentRoot"));
                var reportingOptions = new ReportingServiceOptions()
                {
                    TempFolder = tempFolder,
                };

                services
                    .AddSingleton<IConfiguration>(Configuration)
                    .AddSingleton<IHostedService, ReportingService>(s => new ReportingService(reportingOptions))
                    .AddMvc(options =>
                    {
                        options.InputFormatters.Add(new DataInputFormatter());
                        options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Stream)));
                    }).AddNewtonsoftJson(opts =>
                    {
                        opts.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                        opts.SerializerSettings.Converters.Add(new StringEnumConverter());
                    });

                var urls = Configuration.GetValue<string>("URLS", String.Empty).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                if (urls.Length == 0)
                    urls = Configuration.GetValue<string>("ASPNETCORE_URLS", String.Empty).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                var servers = new List<OpenApiServer>();
                foreach (var url in urls)
                {
                    logger.Trace($"Configurated URL \"{url}\" found.");
                    servers.Add(new OpenApiServer() { Url = $"{url.TrimEnd('/')}/api/v1" });
                }

                var writeDocs = Configuration?.GetValue<bool>("writedocumentation", true) ?? true;

                services
                    .AddSwaggerGen(options =>
                    {
                        options.SwaggerDoc("v1", new OpenApiInfo
                        {
                            Version = "1.0.0",
                            Title = "SER ENGINE REST - Service",
                            Description = "This is the OpenAPI schema from the ser engine rest service.",
                            Contact = new OpenApiContact()
                            {
                                Name = "Sense Excel Reporting",
                                Url = new Uri("http://senseexcel.com"),
                            },
                            License = new OpenApiLicense()
                            {
                                Name = "MIT"
                            }
                        });
                        options.DescribeAllEnumsAsStrings();
                        options.EnableAnnotations();
                        options.IncludeXmlComments($"{Path.Combine(AppContext.BaseDirectory, HostingEnv.ApplicationName)}.xml");
                        options.DocumentFilter<OpenApiDocumentFilter>(servers, writeDocs);
                        options.OperationFilter<OpenApiOperationFilter>();
                    });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The config services section failed.");
            }
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        /// </summary>
        /// <param name="app">Web application builder</param>
        public void Configure(IApplicationBuilder app)
        {
            try
            {
                var counter = Metrics.CreateCounter("PathCounter", "Counts requests to endpoints", new CounterConfiguration
                {
                    LabelNames = new[] { "method", "endpoint" }
                });

                var options = new RewriteOptions()
                   .AddRedirect("(^$)|(index.html)", "swagger/index.html");

                app.UseMvc()
                   .UseMetricServer()
                   .UseDefaultFiles()
                   .UseStaticFiles()
                   .UseSwagger()
                   .UseSwaggerUI(swagOptions =>
                   {
                       swagOptions.SwaggerEndpoint("/swagger/v1/swagger.json", "SER ENGINE REST - Service Documentation");
                   })
                   .UseRewriter(options);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The configuration of the endpiont failed.");
            }
        }
        #endregion
    }
}