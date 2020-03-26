namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Serialization;
    using Ser.Engine.Rest.Filters;
    using Microsoft.OpenApi.Models;
    using System.Collections.Generic;
    using NLog;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using Microsoft.Extensions.Hosting;
    using Ser.Engine.Rest.Services;
    using Microsoft.AspNetCore.Rewrite;
    using Prometheus;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
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
        private readonly IWebHostEnvironment HostingEnv = null;
        private readonly IConfiguration Configuration = null;
        #endregion

        #region Constructor
        /// <summary>
        /// Startup Constructor
        /// </summary>
        /// <param name="env">Hosting Varibales</param>
        /// <param name="configuration">App Configuration</param>
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
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
                    .Configure<KestrelServerOptions>(options =>
                    {
                        options.AllowSynchronousIO = true;
                    })
                    .AddSingleton(Configuration)
                    .AddMvc(options =>
                    {
                        options.InputFormatters.Add(new DataInputFormatter());
                        options.ModelMetadataDetailsProviders.Add(new SuppressChildValidationMetadataProvider(typeof(Stream)));
                        options.EnableEndpointRouting = false;
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
                        //options.DescribeAllEnumsAsStrings();
                        options.EnableAnnotations();
                        options.IncludeXmlComments($"{Path.Combine(AppContext.BaseDirectory, HostingEnv.ApplicationName)}.xml");
                        options.DocumentFilter<OpenApiDocumentFilter>(servers, writeDocs);
                        options.OperationFilter<OpenApiOperationFilter>();
                    })
                    .AddSingleton<IHostedService, ReportingService>(s => new ReportingService(reportingOptions));
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