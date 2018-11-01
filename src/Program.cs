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
                    var sra = ServiceRequestArgs.Create(rargs, rq, true);
                    var result = Service.PostUploadFile(sra);
                    if (result == null)
                        result = "Upload was failed.";
                    rp.AsText(result);
                }, "POST");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Create(rargs, rq, true);
                    var result = Service.PostUploadFile(sra);
                    if (result == null)
                        result = "Upload was failed.";
                    rp.AsText(result);
                }, "POST");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Create(rargs, rq);
                    var data = Service.GetUploadFile(sra);
                    if (data == null)
                        rp.AsText("No Data found.");
                    else
                        rp.AsBytes(rq, data);
                }, "GET");

                Route.Add("/api/v1/file", (rq, rp, rargs) =>
                {
                    var result = Service.DeleteUpload();
                    if (result == null)
                        result = "The deletion failed.";
                    rp.AsText(result);
                }, "DELETE");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Create(rargs);
                    var result = Service.DeleteUpload(sra);
                    if(result == null)
                        result = "The deletion failed.";
                    rp.AsText(result);
                }, "DELETE");
                #endregion

                #region Task Opteration Routes
                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var json = SerRestService.GetRequestTextData(rq);
                    var results = Service.CreateTask(json);

                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "POST");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var results = Service.GetTasks();
                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "GET");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Create(rargs);
                    var results = Service.GetTasks(sra?.Id);
                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "GET");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Create(rargs);
                    rp.AsText(Service.StopTasks(sra?.Id));
                }, "DELETE");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    rp.AsText(Service.StopTasks());
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