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
    using System.Collections.Generic;
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