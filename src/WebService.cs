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
    using Q2g.HelperPem;
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

        /// <summary>
        /// Run as service or not
        /// </summary>
        public static bool RunAsService { get; private set; }

        /// <summary>
        /// Processing Task for Waiting
        /// </summary>
        public Task ProcessTask { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// web service constructor
        /// </summary>
        /// <param name="args">cmd arguments</param>
        /// <param name="runAsService">Run as Service</param>
        public WebService(string[] args, bool runAsService)
        {
            Arguments = args;
            RunAsService = runAsService;
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
                ProcessTask = Task.Run(() =>
                {
                    try
                    {
                        //Build config for webserver
                        var config = new ConfigurationBuilder()
                            .SetBasePath(AppContext.BaseDirectory)
                            .AddEnvironmentVariables()
                            .AddCommandLine(Arguments)
                            .AddJsonFile("appsettings.json", optional: false)
                            .Build();

                        if (RunAsService)
                        {
                            var certificatePathSection = config.GetSection("Kestrel:EndPoints:Https:Certificate:Path");
                            var certificatePasswordSection = config.GetSection("Kestrel:EndPoints:Https:Certificate:Password");
                            if (certificatePathSection.Value == null)
                            {
                                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                                var certFolder = Path.Combine(appdata, "AnalyticsGate", "AGCR", "certificates");
                                var passKey = Path.Combine(certFolder, "AGRRoot.key");
                                var passDat = Path.Combine(certFolder, "AGRRoot.keypas");
                                var crypter = new TextCrypter(passKey);
                                var password = crypter.DecryptText(File.ReadAllText(passDat));
                                certificatePathSection.Value = Path.Combine(certFolder, "AGRRoot.pfx");
                                certificatePasswordSection.Value = password;
                            }
                        }

                        //Start the web server
                        CreateHostBuilder(Arguments)
                             .UseKestrel()
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
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                    }
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