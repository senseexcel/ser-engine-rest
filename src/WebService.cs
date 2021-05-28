namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using NLog;
    using NLog.Web;
    using PeterKottas.DotNetCore.WindowsService.Base;
    using PeterKottas.DotNetCore.WindowsService.Interfaces;
    #endregion

    /// <summary>
    /// Web service class
    /// </summary>
    public class WebService : MicroService, IMicroService
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        private static CancellationTokenSource cts = new CancellationTokenSource();
        /// <summary>
        /// CMD Arguments
        /// </summary>
        public static string[] Arguments { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// web service constructor
        /// </summary>
        /// <param name="args">cmd arguments</param>
        public WebService(string[] args)
        {
            Arguments = args;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Create a new web host builder
        /// </summary>
        /// <param name="args">Arguments</param>
        /// <returns>WebHostBuilder instance</returns>
        private static IWebHostBuilder CreateHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseStartup<Startup>();
        #endregion

        #region Public Methods
        /// <summary>
        /// Start windows service
        /// </summary>
        public void Start()
        {
            try
            {
                Task.Run(() =>
                {
                    //Build config for webserver
                    var config = new ConfigurationBuilder()
                        .SetBasePath(AppContext.BaseDirectory)
                        .AddEnvironmentVariables()
                        .AddCommandLine(Arguments)
                        .Build();

                    //Start the web server
                    CreateHostBuilder(Arguments)
                         .UseKestrel()
                         .ConfigureAppConfiguration((builderContext, config) =>
                         {
                             config.AddJsonFile("appsettings.json", optional: false);
                         })
                         .UseConfiguration(config)
                         .UseContentRoot(Directory.GetCurrentDirectory())
                         .ConfigureLogging(logging =>
                         {
                             logging.ClearProviders();
                             logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                         })
                         .UseNLog()
                         .Build()
                         .Run();
                }, cts.Token);
            }
            catch (Exception ex)
            {
                throw new Exception("Web service startup failed.", ex);
            }
        }

        /// <summary>
        /// Stop windows service
        /// </summary>
        public void Stop()
        {
            try
            {
                logger.Info("Service was stopped...");
                cts.Cancel();
            }
            catch (Exception ex)
            {
                throw new Exception("Web service stopping failed.", ex);
            }
        }
        #endregion
    }
}