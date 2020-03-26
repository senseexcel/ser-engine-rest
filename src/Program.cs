namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using NLog;
    using NLog.Web;
    #endregion

    /// <summary>
    /// Main Program
    /// </summary>
    public class Program
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Main Method
        /// <summary>
        /// Main Method
        /// </summary>
        /// <param name="args">Argumente</param>
        public static void Main(string[] args)
        {
            try
            {
                //Activate Nlog logger with configuration
                var logger = NLogBuilder.ConfigureNLog("App.config").GetCurrentClassLogger();

                //Build config for webserver
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("hosting.json", optional: true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build();
                var contentRoot = config?.GetValue<string>("contentRoot") ?? "wwwroot";
                if (!contentRoot.Contains(":") && !contentRoot.StartsWith("/") && !contentRoot.StartsWith("\\"))
                    contentRoot = Path.Combine(AppContext.BaseDirectory, contentRoot);
                Directory.CreateDirectory(contentRoot);

                //Start the web server
                CreateWebHostBuilder(args)
                    .UseKestrel()
                    .UseContentRoot(contentRoot)
                    .UseConfiguration(config)
                    .ConfigureLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    })
                    .UseNLog()
                    .Build()
                    .Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Webserver has fatal error.");
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Start WebServer
        /// </summary>
        /// <param name="args">Argumente</param>
        /// <returns></returns>
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
        #endregion
    }
}