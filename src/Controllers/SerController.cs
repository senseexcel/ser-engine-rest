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
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Ser.Engine;
    using Ser.Engine.Jobs;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using System.Web;
    using System.Net;
    using Newtonsoft.Json;
    using System.Threading;
    using Ser.Api;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using System.ComponentModel;
    using Newtonsoft.Json.Serialization;
    using System.Collections.Concurrent;
    using Ser.Engine.Rest.Services;
    using Microsoft.Extensions.Hosting;
    #endregion

    /// <summary>
    /// Main service class
    /// </summary>
    public class SerController : Controller
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properites && Variables
        private ReportingService serService = null;
        #endregion

        #region Constructor
        /// <summary>
        /// Main Controller
        /// </summary>
        /// <param name="configuration">Confi</param>
        /// <param name="service">Reporting service</param>
        public SerController(IConfiguration configuration, IHostedService service)
        {
            logger.Debug("Create new ser controller");
            if (service == null)
                throw new Exception("The reporting service ist null.");

            if (service is ReportingService reportingService)
                serService = reportingService;

            if (serService == null)
                throw new Exception("The reporting service is not found.");
        }
        #endregion

        #region Private Methods
        private byte[] GetBytesFromStream(Stream stream)
        {
            try
            {
                stream.Position = 0;
                var length = stream.Length;
                var buffer = new byte[length];
                stream.Read(buffer, 0, buffer.Length);
                stream.Close();
                return buffer;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The stream could not read.");
                return null;
            }
        }
        #endregion

        #region Genral Methods
        /// <summary>
        /// Log the excpetion with Nlog and create a response
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="message">Message</param>
        /// <param name="result">Result</param>
        /// <returns></returns>
        protected BadRequestObjectResult GetBadRequestAndLog(Exception exception, string message, OperationResult result = null)
        {
            logger.Error(exception, message);
            if (result == null)
                return new BadRequestObjectResult(new OperationResult() { Success = false, Error = message });
            else
                return new BadRequestObjectResult(result);
        }
        #endregion

        #region File Methods
        /// <summary>
        /// Upload file
        /// </summary>
        /// <param name="filename">Name of the file</param>
        /// <param name="data">File as stream</param>
        /// <param name="fileId">Id of the folder</param>
        /// <param name="unzip">unzip zip files</param>
        /// <returns></returns>
        protected OperationResult PostUploadFile(string filename, Stream data, bool unzip = false, Guid? fileId = null)
        {
            var newGuid = Guid.NewGuid();
            try
            {
                if (fileId != null)
                {
                    logger.Debug($"Guid {fileId} was found.");
                    newGuid = fileId.Value;
                }

                if (String.IsNullOrEmpty(filename))
                {
                    var msg = "No filename found.";
                    logger.Error(msg);
                    return new OperationResult() { Success = false, Error = msg };
                }

                var fileData = GetBytesFromStream(data);
                if (fileData == null)
                {
                    var msg = "No file uploaded.";
                    logger.Error(msg);
                    return new OperationResult() { Success = false, Error = msg };
                }

                serService.WriteUploadFile(newGuid, fileData, filename, unzip);
                return new OperationResult()
                {
                    Success = true,
                    OperationId = newGuid,
                };
            }
            catch (Exception ex)
            {
                var msg = $"The upload with id {newGuid} was failed.";
                logger.Error(ex, msg);
                return new OperationResult()
                {
                    Success = false,
                    Error = msg
                };
            }
        }

        /// <summary>
        /// Get the file from the upload folder
        /// </summary>
        /// <param name="fileId">Id of the folder</param>
        /// <param name="filename">Name of the file</param>
        /// <returns></returns>
        protected byte[] GetUploadFile(Guid? fileId, string filename = null)
        {
            try
            {
                if (!fileId.HasValue)
                    throw new Exception("Upload id required. - No id");

                //single file
                var uploadPath = String.Empty;
                if (!String.IsNullOrEmpty(filename))
                {
                    uploadPath = Path.Combine(serService.Options.TempFolder, fileId.Value.ToString(), filename);
                    if (System.IO.File.Exists(uploadPath))
                    {
                        logger.Debug($"Find file {uploadPath}");
                        return System.IO.File.ReadAllBytes(uploadPath);
                    }
                }

                //all files
                uploadPath = Path.Combine(serService.Options.TempFolder, fileId.Value.ToString());
                var zipPath = Path.Combine(serService.Options.TempFolder, $"{Guid.NewGuid().ToString()}.zip");
                logger.Info($"Create zip file \"{zipPath}\"");
                ZipFile.CreateFromDirectory(uploadPath, zipPath, CompressionLevel.Fastest, false);
                var zipData = System.IO.File.ReadAllBytes(zipPath);
                logger.Debug($"File Size: {zipData.Length}");
                logger.Debug($"Remove zip file \"{zipPath}\"");
                System.IO.File.Delete(zipPath);
                logger.Debug("Finish to send.");
                return zipData;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The call failed.");
                return null;
            }
        }

        /// <summary>
        /// Delete upload files
        /// </summary>
        /// <param name="fileId">Id of the folder</param>
        /// <returns>Return the status</returns>
        protected OperationResult DeleteUpload(Guid? fileId = null)
        {
            try
            {
                serService.DeleteFile(fileId);
                return new OperationResult() { Success = true };
            }
            catch (Exception ex)
            {
                var msg = "The deletion failed.";
                logger.Error(ex, msg);
                return new OperationResult()
                {
                    Success = false,
                    Error = msg
                };
            }
        }
        #endregion

        #region Task Methods
        /// <summary>
        /// Create a new task
        /// </summary>
        /// <param name="jsonJobContent">Json-Job file of SER.</param>
        /// <param name="taskId">The id for the task process.</param>
        /// <returns>Return a task id</returns>
        protected OperationResult PostStartTask(string jsonJobContent, Guid? taskId = null)
        {
            try
            {
                logger.Trace($"JSONREQUEST: {jsonJobContent}");
                if (String.IsNullOrEmpty(jsonJobContent))
                {
                    var msg = "The json request was emtpy.";
                    logger.Error(msg);
                    return new OperationResult()
                    {
                        Success = false,
                        Error = msg
                    };
                }

                if (!taskId.HasValue)
                    taskId = Guid.NewGuid();

                serService.RunNewTask(taskId.Value, jsonJobContent);

                return new OperationResult()
                {
                    Success = true,
                    OperationId = taskId,
                };
            }
            catch (Exception ex)
            {
                var msg = "The task could not created.";
                logger.Error(ex, msg);
                return new OperationResult()
                {
                    Success = false,
                    Error = msg
                };
            }
        }

        /// <summary>
        /// Get the status of a Task.
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <param name="taskStatus">Get tasks only with this status.</param>
        /// <returns>Return the result json of running task.</returns>
        protected OperationResult GetTasks(Guid? taskId = null, TaskStatusInfo? taskStatus = null)
        {
            try
            {
                var results = serService?.GetTasks(taskId, taskStatus) ?? new List<JobResult>();
                return new OperationResult()
                {
                    Success = true,
                    Results = results
                };
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task result could not found.");
                return new OperationResult()
                {
                    Success = false,
                    Results = new List<JobResult>()
                };
            }
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <returns>Return a status</returns>
        protected OperationResult DeleteTasks(Guid? taskId = null)
        {
            try
            {
                serService.StopTasks(taskId);
                return new OperationResult() { Success = true };
            }
            catch (Exception ex)
            {
                var msg = "The engine could not be stopped properly.";
                logger.Error(ex, msg);
                return new OperationResult()
                {
                    Success = false,
                    Error = msg
                };
            }
        }
        #endregion

        #region Health
        /// <summary>
        /// Get the health status form service.
        /// </summary>
        /// <returns>result</returns>
        protected OperationResult GetHealthStatus()
        {
            try
            {
                var count = serService?.WorkingCount ?? 0;
                var health = "ready";
                if (count > 0)
                    health = "working";
                return new OperationResult()
                {
                    Success = true,
                    Health = health,
                    WorkingCount = count
                };
            }
            catch (Exception ex)
            {
                var msg = "The health status failed.";
                logger.Error(ex, msg);
                return new OperationResult()
                {
                    Success = false,
                    Error = msg,
                    Health = "unknown",
                    WorkingCount = 0
                };
            }
        }

        #endregion
    }

    #region Respose Helper Class
    /// <summary>
    /// Result object of the rest service operation
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore, NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class OperationResult
    {
        /// <summary>
        /// Result of operation
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Error { get; set; }

        /// <summary>
        /// Id for the operation
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Guid? OperationId { get; set; }

        /// <summary>
        /// Status of service
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Health { get; set; }

        /// <summary>
        /// Count of job are running
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int WorkingCount { get; set; }

        /// <summary>
        /// List an reports
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<JobResult> Results { get; set; }
    }
    #endregion
}