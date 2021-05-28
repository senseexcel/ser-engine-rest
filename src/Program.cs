namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using NLog;
    using NLog.Web;
    using PeterKottas.DotNetCore.WindowsService;
    #endregion

    /// <summary>
    /// Main Program
    /// </summary>
    public class Program
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Public Methods
        /// <summary>
        /// Main entry method
        /// </summary>
        /// <param name="args">Arguments</param>
        public static void Main(string[] args)
        {
            try
            {
                //Activate Nlog logger with configuration
                logger = NLogBuilder.ConfigureNLog("App.config").GetCurrentClassLogger();

                // Use as Windows Service
                ServiceRunner<WebService>.Run(config =>
                {
                    config.SetDisplayName("AnalyticsGate Rest Service");
                    config.SetDescription("Rest Service for AnalyticsGate Reporting");
                    var name = config.GetDefaultName();
                    config.Service(serviceConfig =>
                    {
                        serviceConfig.ServiceFactory((extraArguments, controller) =>
                        {
                            var webService = new WebService(args);
                            return webService;
                        });
                        serviceConfig.OnStart((service, extraParams) =>
                        {
                            logger.Debug($"Service {name} started...");
                            service.Start();
                        });
                        serviceConfig.OnStop(service =>
                        {
                            logger.Debug($"Service {name} stopped...");
                            service.Stop();
                        });
                        serviceConfig.OnError(ex =>
                        {
                            logger.Error($"Service Exception: {ex}");
                        });
                    });
                });

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Cloud connector service has fatal error.");
                Environment.Exit(1);
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// Create a new web host builder
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>WebHostBuilder instance</returns>
        public static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
        #endregion
    }
}