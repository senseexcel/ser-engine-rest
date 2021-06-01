namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
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

        #region Private Methods
        private static string Getversion(Version version)
        {
            if (version == null)
                return "unknown";
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
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

                if (args.Length > 0 && args[0] == "VersionNumber")
                {
                    var appVersion = Getversion(Assembly.GetExecutingAssembly().GetName().Version);
                    File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Version.txt"), appVersion);
                    return;
                }

                var runAsService = true;
                if (args.Length > 0 && args[0] == "--Mode=NoService")
                {
                    logger.Debug("Run Nativ...");
                    runAsService = false;
                    var argList = args.ToList();
                    argList.RemoveAt(0);
                    args = argList.ToArray();
                }

                if (runAsService)
                {
                    // Use as Windows Service
                    logger.Debug("Run as service...");
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
                }
                else
                {
                    var webService = new WebService(args);
                    webService.Start();
                    webService.ProcessTask.Wait();
                }

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