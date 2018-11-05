namespace Ser.Engine.Rest
{
    #region Usings
    using Fclp;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    #endregion

    public class ServiceParameter
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        public bool UseHttps { get; set; }
        public int Port { get; set; } = 80;
        public string TempDir { get; set; }
        #endregion

        #region Constructor
        public ServiceParameter() {}

        public ServiceParameter(string[] args)
        {
            var appArgs = new FluentCommandLineParser<ServiceParameter>()
            {
                IsCaseSensitive = false,
            };
            appArgs.Setup<bool>(arg => arg.UseHttps)
                    .As('h', "usehttps")
                    .WithDescription("Use https for connection.")
                    .SetDefault(false);
            appArgs.Setup<int>(arg => arg.Port)
                    .As('p', "port")
                    .WithDescription("The port for listening.")
                    .SetDefault(11271);
            appArgs.Setup<string>(arg => arg.TempDir)
                    .As('w', "workdir")
                    .WithDescription("The path to the work dir.")
                    .SetDefault(null);
            var result = appArgs.Parse(args);
            if (result.HasErrors)
            {
                logger.Error("The arguments are not correct.");
                return;
            }

            UseHttps = appArgs.Object.UseHttps;
            Port = appArgs.Object.Port;
            if (Directory.Exists(appArgs.Object.TempDir))
                TempDir = appArgs.Object.TempDir;
            else
            {
                TempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp");
                Directory.CreateDirectory(TempDir);
            }
        }
        #endregion
    }
}