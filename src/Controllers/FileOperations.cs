#region License
/*
Copyright (c) 2019 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Engine.Rest.Controllers
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;
    using Ser.Engine.Rest.Attributes;
    using Swashbuckle.AspNetCore.Annotations;
    using Microsoft.Extensions.Configuration;
    using NLog;
    using Microsoft.AspNetCore.Http;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http.Internal;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.Hosting;
    #endregion

    /// <summary>
    /// Controller for file operations
    /// </summary>
    public class FileOperationsApiController : SerController
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Constructor
        /// <summary>
        /// Construtor with configuration
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="service">Reporting service</param>
        public FileOperationsApiController(IConfiguration configuration, IHostedService service) : base(configuration, service) { }
        #endregion

        #region Public Methods
        /// <summary>
        /// Upload a file to the service with a fixed file id.
        /// </summary>
        /// <param name="data">The file data to upload.</param>
        /// <param name="filename">The Name of the file</param>
        /// <param name="fileId">The file id for the created folder.</param>
        /// <param name="unzip">Unpacking zip files after upload.</param>
        /// <response code="200">Returns the transfered file id.</response>
        [HttpPost]
        [Route("/api/v1/file/{fileId}")]
        [ValidateModelState]
        [Consumes("application/octet-stream")]
        [Produces("application/json")]
        [SwaggerOperation("UploadWithId", "Upload a file to the service with a fixed file id.", Tags = new[] { "Upload File" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns the transfered file id.")]
        public virtual IActionResult UploadWithId([FromBody][Required] Stream data, [FromRoute][Required] Guid fileId, [FromHeader][Required] string filename, [FromHeader] bool unzip = false)
        {
            try
            {
                logger.Trace($"Start upload file - Id: {fileId}");
                var result = PostUploadFile(filename, data, unzip, fileId);
                return GetRequestAndLog(nameof(UploadWithId), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(UploadWithId)} failed.");
            }
        }

        /// <summary>
        /// Upload a file to the service.
        /// </summary>
        /// <param name="data">The file data to upload.</param>
        /// <param name="filename">The Name of the file</param>
        /// <param name="unzip">Unpacking zip files after upload.</param>
        /// <response code="200">Returns a new generated file id.</response>
        [HttpPost]
        [Route("/api/v1/file")]
        [ValidateModelState]
        [Consumes("application/octet-stream")]
        [Produces("application/json")]
        [SwaggerOperation("Upload", "Upload a file to the service.", Tags = new[] { "Upload File" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns a new generated file id.")]
        public virtual IActionResult Upload([FromBody][Required] Stream data, [FromHeader][Required] string filename, [FromHeader] bool unzip = false)
        {
            try
            {
                logger.Trace("Start upload file");
                var result = PostUploadFile(filename, data, unzip);
                return GetRequestAndLog(nameof(Upload), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(Upload)} failed.");
            }
        }

        /// <summary>
        /// Get the files from the service.
        /// </summary>
        /// <param name="fileId">The file id that has been created.</param>
        /// <param name="filename">Special file to be returned.</param>
        /// <response code="200">Returns the file from id.</response>
        [HttpGet]
        [Route("/api/v1/file/{fileId}")]
        [ValidateModelState]
        [Produces("application/octet-stream")]
        [SwaggerOperation("DownloadFiles", "Get the files from the service.", Tags = new[] { "Download File" })]
        [SwaggerResponse(statusCode: 200, type: typeof(IFormFile), description: "Returns the file(s) from id.")]
        public virtual IActionResult DownloadFiles([FromRoute][Required] Guid fileId, [FromHeader] string filename)
        {
            try
            {
                logger.Trace($"Start get file content - Id: {fileId} Filename: {filename}");
                var data = GetUploadFile(fileId, filename);
                logger.Trace($"{nameof(DownloadFiles)} - Response file data length: {data.Length}");
                return File(data, "application/octet-stream", "download.zip");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The Method {nameof(DownloadFiles)} failed.");
                return new ObjectResult(null);
            }
        }

        /// <summary>
        /// Delete a file in the service.
        /// </summary>
        /// <param name="fileId">Special folder to be deleted.</param>
        /// <response code="200">Returns the operation status.</response>
        [HttpDelete]
        [Route("/api/v1/file/{fileId}")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("DeleteFiles", "Delete a file in the service.", Tags = new[] { "Delete Files" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns the operation status.")]
        public virtual IActionResult DeleteFiles([FromRoute][Required] Guid fileId)
        {
            try
            {
                logger.Trace($"Start delete file content - Id: {fileId}");
                var result = DeleteUpload(fileId);
                return GetRequestAndLog(nameof(DeleteFiles), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(DeleteFiles)} failed.");
            }
        }

        /// <summary>
        /// Delete files in the service.
        /// </summary>
        /// <response code="200">Returns the operation status.</response>
        [HttpDelete]
        [Route("/api/v1/file")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("DeleteAllFiles", "Delete files in the service.", Tags = new[] { "Delete Files" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns the operation status.")]
        public virtual IActionResult DeleteAllFiles()
        {
            try
            {
                logger.Trace($"Start delete file content");
                var result = DeleteUpload();
                return GetRequestAndLog(nameof(DeleteAllFiles), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(DeleteAllFiles)} failed.");
            }
        }
        #endregion
    }
}