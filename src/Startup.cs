namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OpenApi.Models;
    using NLog;
    using Microsoft.Extensions.Hosting;
    using Ser.Engine.Rest.Services;
    using Microsoft.AspNetCore.Hosting;
    using System.Reflection;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using Ser.Engine.Rest.Model;
    #endregion

    /// <summary>
    /// Startup class of the service
    /// </summary>
    public class Startup
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties And Variables
        private readonly IConfiguration Configuration = null;
        #endregion

        #region Constructor
        /// <summary>
        /// Startup Constructor
        /// </summary>
        /// <param name="configuration">App Configuration</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Clenup temp folder
        /// </summary>
        /// <param name="folder">temporary folder</param>
        public static void SoftCleanup(string folder)
        {
            try
            {
                Directory.Delete(folder, true);
            }
            catch
            {
                logger.Info($"Cloud not delete folder '{folder}'...");
            }
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
                // Check for corupted xml key files from asp.net core
                var aspKeyRingFolder = Environment.ExpandEnvironmentVariables(@"%LocalAppData%\ASP.net\DataProtection-Keys");
                if (Directory.Exists(aspKeyRingFolder))
                {
                    var xmlKeyRingFiles = Directory.GetFiles(aspKeyRingFolder, "*.xml", SearchOption.TopDirectoryOnly);
                    foreach (var xmlKeyRingFile in xmlKeyRingFiles)
                    {
                        var byteCount = File.ReadAllBytes(xmlKeyRingFile)?.Length ?? 0;
                        if (byteCount <= 3)
                        {
                            logger.Info("Broken key ring file found. This has been removed.");
                            File.Delete(xmlKeyRingFile);
                        }
                    }
                }

                var tempFolder = Configuration.GetValue<string>(WebHostDefaults.ContentRootKey);
                if (tempFolder.TrimEnd('/', '\\') == AppContext.BaseDirectory.TrimEnd('/', '\\'))
                {
                    tempFolder = Path.Combine(Path.GetTempPath(), "RestService");
                    if (Directory.Exists(tempFolder))
                        SoftCleanup(tempFolder);
                }
                Directory.CreateDirectory(tempFolder);

                var reportingOptions = new ReportingServiceOptions() { TempFolder = tempFolder };
                services.AddSingleton<IReportingService, ReportingService>(s => new ReportingService(reportingOptions));

                var fileHostingOptions = new FileHostingOptions() { TempFolder = tempFolder };
                services.AddSingleton<IFileHostingService, FileHostingService>(s => new FileHostingService(fileHostingOptions));

                services.AddControllers();

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AGReportingService", Version = "v1" });
                    c.IncludeXmlComments(xmlPath, true);
                    c.CustomOperationIds(apiDesc => apiDesc.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The config services section failed.");
            }
        }

        /// <summary>
        /// The Configure method is used to specify how the app responds to HTTP requests.
        /// </summary>
        /// <param name="app">application infos</param>
        /// <param name="env">enviroment infos</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            try
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AGReportingService v1");
                });
                app.UseRouting();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The configuration of the endpiont failed.");
            }
        }
        #endregion
    }
}