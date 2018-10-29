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
        private static SerService Service = null;
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
                Service = new SerService(config);
                Route.Before = (rq, rp) => { logger.Info($"Requested: {rq.Url.PathAndQuery}"); return false; };

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var results = Service.GetTasks();
                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "GET");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Parse(rargs);
                    var results = Service.GetTasks(sra.Id);
                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "GET");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    var json = GetRequestTextData(rq);
                    var results = Service.CreateTask(json);
                    rp.AsText(JsonConvert.SerializeObject(results));
                }, "POST");

                Route.Add("/api/v1/task", (rq, rp, rargs) =>
                {
                    rp.AsText(Service.StopTasks());
                }, "DELETE");

                Route.Add("/api/v1/task/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Parse(rargs);
                    rp.AsText(Service.StopTasks(sra.Id));
                }, "DELETE");

                Route.Add("/api/v1/file", (rq, rp, rargs) =>
                {
                    var fileData = GetRequestFileData(rq);
                    var sra = ServiceRequestArgs.Parse(rargs);
                    rp.AsText(Service.UploadFile(fileData, sra));
                }, "POST");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var fileData = GetRequestFileData(rq);
                    var sra = ServiceRequestArgs.Parse(rargs);
                    rp.AsText(Service.UploadFile(fileData, sra));
                }, "POST");

                Route.Add("/api/v1/file", (rq, rp, rargs) =>
                {
                    rp.AsText(Service.DeleteUploads());
                }, "DELETE");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Parse(rargs);
                    rp.AsText(Service.DeleteUploads(sra));
                }, "DELETE");

                Route.Add("/api/v1/file/{id}", (rq, rp, rargs) =>
                {
                    var sra = ServiceRequestArgs.Parse(rargs);
                    var path = Service.GetTaskResult(sra.Id);
                    rp.AsFile(rq, path);
                }, "GET");

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

        #region Private Methods
        private static byte[] GetRequestFileData(HttpListenerRequest request)
        {
            try
            {
                if (!request.HasEntityBody)
                    return null;

                var mem = new MemoryStream();
                request.InputStream.CopyTo(mem);
                return mem.ToArray();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request {request} could not readed.");
                return null;
            }
        }

        private static string GetRequestTextData(HttpListenerRequest request)
        {
            try
            {
                if (!request.HasEntityBody)
                    return null;

                using (Stream body = request.InputStream)
                {
                    using (StreamReader reader = new StreamReader(body, request.ContentEncoding))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request {request} could not readed.");
                return null;
            }
        }
        #endregion

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