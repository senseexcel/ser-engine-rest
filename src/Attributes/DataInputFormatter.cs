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
    using Microsoft.AspNetCore.Http.Internal;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
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
                req.EnableRewind();
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