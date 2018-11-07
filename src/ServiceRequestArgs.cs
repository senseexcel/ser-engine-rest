namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Web;
    using NLog;
    #endregion

    public class ServiceRequestArgs
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        public Guid? Id { get; set; }
        public bool Unzip { get; set; }
        public string Filename { get; set; }
        public string CopyFolder { get; set; }
        public byte[] PostData { get; set; }
        public string PostText { get; set; }
        #endregion

        #region Private Methods
        private static T GetValueFromQuery<T>(string url, string name)
        {
            try
            {
                url = HttpUtility.UrlDecode(url);
                url = url.TrimStart('/');
                var uri = new UriBuilder($"http://localhost/{url}");
                var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                var value = queryDictionary[name] ?? null;
                value = value?.Trim('\'');
                value = value?.Trim('\"');
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Can´t read the uri query {url}.");
                return default(T);
            }
        }

        private static string GetQueryValue(string value)
        {
            value = value?.Trim('\'');
            value = value?.Trim('\"');
            return value;
        }

        private static Guid? ParseGuid(string value)
        {
            if(Guid.TryParse(value, out var id))
                return id;
            return null;
        }

        private static string ParseQuery(string value, ServiceRequestArgs sra)
        {
            var split = value.Split('?');
            if (split.Length == 2)
            {
                logger.Debug($"Query {split[1]} found.");
                var queryDictionary = HttpUtility.ParseQueryString(split[1]);
                sra.Filename = GetQueryValue(queryDictionary["filename"]);
                Boolean.TryParse(GetQueryValue(queryDictionary["unzip"]), out var unzip);
                sra.Unzip = unzip;
            }
            else if (split.Length > 2)
                logger.Error("Too many chars '?' found.");
            return split[0];
        }

        private static byte[] GetRequestFileData(HttpListenerRequest request)
        {
            var mem = new MemoryStream();
            try
            {
                if (!request.HasEntityBody)
                {
                    logger.Warn("The content is empty.");
                    return null;
                }

                request.InputStream.CopyTo(mem);
                var data = mem.ToArray();
                return data;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request {request} could not readed.");
                return null;
            }
            finally
            {
                mem.Close();
                mem.Dispose();
            }
        }

        private static string GetRequestTextData(HttpListenerRequest request)
        {
            try
            {
                if (!request.HasEntityBody)
                {
                    logger.Warn("The text content is empty.");
                    return null;
                }

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

        #region Publlic Methods
        public static ServiceRequestArgs FromFile(Dictionary<string, string> requestArgs, HttpListenerRequest request = null)
        {
            var result = Create(requestArgs, request);
            if (request != null)
            {
                var fileValue = request.Headers["SerFilename"];
                if (String.IsNullOrEmpty(fileValue))
                {
                    if (request.HttpMethod.ToUpperInvariant() == "POST")
                    {
                        logger.Warn("The filename is empty.");
                        return null;
                    }
                    else
                        logger.Debug("The filename is empty.");
                }
                result.Filename = fileValue;
                if (request.HttpMethod.ToUpperInvariant() == "POST")
                    result.PostData = GetRequestFileData(request);
            }
            var zipMode = request?.Headers["SerUnzip"]?.ToLowerInvariant() ?? "false";
            result.Unzip = Boolean.Parse(zipMode);

            var copyFolder = request?.Headers["SerCopyFolder"]?.ToLowerInvariant() ?? null;
            result.CopyFolder = copyFolder;

            return result;
        }

        public static ServiceRequestArgs FromTask(Dictionary<string, string> requestArgs, HttpListenerRequest request = null)
        {
            var result = Create(requestArgs, request);
            if (request != null)
            {
                if (request.HttpMethod.ToUpperInvariant() == "POST")
                    result.PostText = GetRequestTextData(request);
            }
            return result;
        }

        private static ServiceRequestArgs Create(Dictionary<string, string> requestArgs, HttpListenerRequest request = null)
        {
            try
            {
                var result = new ServiceRequestArgs();
                logger.Debug($"{requestArgs.Count} arguments found.");
                foreach (var arg in requestArgs)
                {
                    switch (arg.Key)
                    {
                        case "id":
                            var resultid = ParseQuery(arg.Value, result);
                            result.Id = ParseGuid(resultid);
                            break;
                        default:
                            break;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The arguments could not parse.");
                return null;
            }
        }
        #endregion
    }
}