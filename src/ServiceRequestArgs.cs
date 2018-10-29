namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        #endregion

        #region Publlic Methods
        public static ServiceRequestArgs Parse(Dictionary<string, string> requestArgs)
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