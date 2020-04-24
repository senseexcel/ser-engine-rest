namespace Ser.Engine.Rest
{
    #region Usings
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.AspNetCore.Http;
    #endregion

    /// <summary>
    /// Read and convert file data to a byte array
    /// </summary>
    public class DataInputFormatter : IInputFormatter
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private readonly List<string> _allowedMimeTypes = new List<string>
        {
            "application/pdf",
            "application/zip",
            "application/octet-stream"
        };
        #endregion

        #region Public Functions
        /// <summary>
        /// Read stream and analysis mime type
        /// </summary>
        /// <param name="context">context</param>
        /// <returns></returns>
        public bool CanRead(InputFormatterContext context)
        {
            try
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(context));
                var contentType = context.HttpContext.Request.ContentType;
                if (_allowedMimeTypes.Any(x => x.Contains(contentType)))
                    return true;
                return false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "DataInputFormatter - Method Canread failed.");
                return false;
            }
        }

        /// <summary>
        /// Read data and convert to byte array
        /// </summary>
        /// <param name="context">context</param>
        /// <returns></returns>
        public Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            try
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(context));
                var req = context.HttpContext.Request;
                HttpRequestRewindExtensions.EnableBuffering(req);
                var memoryStream = new MemoryStream();
                context.HttpContext.Request.Body.CopyTo(memoryStream);
                req.Body.Seek(0, SeekOrigin.Begin);
                return InputFormatterResult.SuccessAsync(memoryStream);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "DataInputFormatter - Method ReadAsync failed.");
                return InputFormatterResult.FailureAsync();
            }
        }
        #endregion
    }
}