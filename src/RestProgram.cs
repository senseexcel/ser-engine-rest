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
    #endregion

    public class RestProgram
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        /// <summary>
        /// CMD Arguments
        /// </summary>
        public static string[] Arguments { get; private set; }
        #endregion

        #region Constrcutor
        public RestProgram(string[] args)
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

        #region Public Method
        /// <summary>
        /// Start Service
        /// </summary>
        public void Start()
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
        }
        #endregion
    }
}