namespace Ser.Engine.Rest
{ 
    #region Usings
    using NLog;
    using NLog.Config;
    using SimpleHttp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Web;
    using Newtonsoft.Json;
    #endregion

    class Program
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        private static SerRestService Service = null;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                SetLoggerSettings("App.config");
                logger.Info("Initialize Server...");
                var config = new ServiceParameter(args);
                Directory.CreateDirectory(config.TempDir);
                logger.Debug($"Temp folder: \"{config.TempDir}\"");
                Service = new SerRestService(config);
                Route.Before = (rq, rp) => { logger.Info($"Requested: {rq.Url.PathAndQuery}"); return false; };

                #region File Opteration Routes
                Route.Add("/api/v1/file", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromFile(rargs, rq);
                    var result = Service.PostUploadFile(sra);
                    if (result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "POST");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromFile(rargs, rq);
                    var result = Service.PostUploadFile(sra);
                    if (result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "POST");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromFile(rargs, rq);
                    var data = Service.GetUploadFile(sra);
                    if (data == null)
                        rp.AsText(JsonConvert.SerializeObject("ERROR"));
                    else
                        rp.AsBytes(rq, data);
                }, "GET");

                Route.Add("/api/v1/file", (rq, rp, rargs) =>
                {
                    var result = Service.DeleteUpload();
                    if (result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "DELETE");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromFile(rargs);
                    var result = Service.DeleteUpload(sra);
                    if(result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "DELETE");
                #endregion

                #region Task Opteration Routes
                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromTask(rargs, rq);
                    var result = Service.CreateTask(sra);
                    if (result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "POST");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var result = Service.GetTasks();
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "GET");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromTask(rargs);
                    var result = Service.GetTasks(sra);
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "GET");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.FromTask(rargs);
                    var result = Service.StopTasks(sra);
                    if (result == null)
                        result = "ERROR";
                    rp.AsText(JsonConvert.SerializeObject(result));
                }, "DELETE");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    rp.AsText(JsonConvert.SerializeObject(Service.StopTasks()));
                }, "DELETE");
                #endregion

                logger.Info($"Server in running on port \"{config.Port}\"...");
                var cts = new CancellationTokenSource();
                var server = HttpServer.ListenAsync(config.Port, cts.Token, Route.OnHttpRequestAsync, config.UseHttps);
                AppExit.WaitFor(cts, server);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The web server has an fatal error.");
            }
        }

        #region nlog helper for netcore
        private static void SetLoggerSettings(string configName)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName);
            if (!File.Exists(path))
            {
                var root = new FileInfo(path).Directory?.Parent?.Parent?.Parent;
                var files = root.GetFiles("App.config", SearchOption.AllDirectories).ToList();
                path = files.FirstOrDefault()?.FullName;
            }

            LogManager.Configuration = new XmlLoggingConfiguration(path, false);
        }
        #endregion
    }
}