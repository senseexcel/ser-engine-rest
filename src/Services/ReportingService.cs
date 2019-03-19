#region License
/*
Copyright (c) 2019 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Engine.Rest.Services
{
    #region Usings
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Ser.Api;
    using Ser.Engine.Jobs;
    #endregion

    /// <summary>
    /// Reporting service
    /// </summary>
    public class ReportingService : BackgroundService
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        private ConcurrentDictionary<Guid, JobManager> managerPool;

        /// <summary>
        /// Reporting Options
        /// </summary>
        public ReportingServiceOptions Options { get; set; }

        /// <summary>
        /// Current working jobs
        /// </summary>
        public int WorkingCount { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize reporting service
        /// </summary>
        /// <param name="options">options</param>
        public ReportingService(ReportingServiceOptions options)
        {
            Options = options ?? throw new Exception("No reporting options found.");
            managerPool = new ConcurrentDictionary<Guid, JobManager>();
            WorkingCount = 0;
        }
        #endregion

        #region Private Methods
        private AppParameter GetJobParameter(string workdir)
        {
            var args = new string[] { $"--workdir={workdir}" };
            return new AppParameter(args);
        }

        private JobManager CreateManager(string workdir)
        {
            return new JobManager(GetJobParameter(workdir));
        }

        private void CopyFiles(string sourceFolder, string targetFolder)
        {
            try
            {
                if (!Directory.Exists(sourceFolder))
                    throw new Exception($"The Folder {sourceFolder} does not exits.");
                var copyFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories);
                foreach (var copyFile in copyFiles)
                {
                    if (Path.GetExtension(copyFile).ToLowerInvariant() == ".zip")
                        continue;
                    var relPath = copyFile.Replace($"{sourceFolder}{Path.DirectorySeparatorChar}", "");
                    var destFile = Path.Combine(targetFolder, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    logger.Debug($"Copy File: {copyFile} to {destFile}");
                    System.IO.File.Copy(copyFile, destFile, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Copy Progress from {sourceFolder} to {targetFolder} failed.");
            }
        }

        private void RunTask(Guid taskId, string jobJson)
        {
            try
            {
                logger.Debug($"START - {taskId}");

                var taskFolder = Path.Combine(Options.TempFolder, taskId.ToString());
                Directory.CreateDirectory(taskFolder);
                var jObject = JObject.Parse(jobJson) as dynamic;
                JArray guidArray = jObject?.uploadGuids ?? null;
                if (guidArray != null)
                {
                    foreach (var item in guidArray)
                    {
                        var uploadFolder = Path.Combine(Options.TempFolder, item.Value<string>());
                        logger.Debug($"Copy file from {uploadFolder} to {taskFolder}.");
                        CopyFiles(uploadFolder, taskFolder);
                    }
                }

                logger.Debug($"The Task {taskId} was started.");
                var manager = CreateManager(taskFolder);
                managerPool.TryAdd(taskId, manager);
                manager.Run(jobJson);
                logger.Debug($"The Task {taskId} was finished.");
                managerPool.TryRemove(taskId, out manager);
                WorkingCount--;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The task {taskId} failed.");
            }
        }

        private void Delete(Guid? id = null)
        {
            var folders = new List<string>();
            if (id.HasValue)
                folders.Add(Path.Combine(Options.TempFolder, id.ToString()));
            else
                folders.AddRange(Directory.GetDirectories(Options.TempFolder, "*.*", SearchOption.TopDirectoryOnly));

            foreach (var folder in folders)
            {
                try
                {
                    logger.Debug($"Delete folder {folder}");
                    Directory.Delete(folder, true);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"The folder {folder} could not delete.");
                }
            }

            logger.Debug("The deletion was completed.");
        }

        private void StopAllJobs()
        {
            logger.Debug("Stop all jobs.");
            foreach (var manager in managerPool)
                manager.Value?.Stop();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Write uploaded file to folder
        /// </summary>
        /// <param name="fileId">Name of the file</param>
        /// <param name="fileData">File as stream</param>
        /// <param name="filename">Id of the folder</param>
        /// <param name="unzip">unzip zip files</param>
        public void WriteUploadFile(Guid fileId, byte[] fileData, string filename, bool unzip)
        {
            logger.Debug($"Upload file - ID: {fileId} Name: {filename} Unzip: {unzip}");
            Task.Run(() =>
            {
                try
                {
                    var uploadFolder = Path.Combine(Options.TempFolder, fileId.ToString());
                    Directory.CreateDirectory(uploadFolder);
                    var fullname = Path.Combine(uploadFolder, filename);
                    System.IO.File.WriteAllBytes(fullname, fileData);
                    if (unzip)
                    {
                        logger.Debug($"Unzip file {fullname}");
                        ZipFile.ExtractToDirectory(fullname, uploadFolder, true);
                    }
                    logger.Debug($"Upload {fileId} successfully.");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Upload {fileId} failed.");
                }
            });
        }

        /// <summary>
        /// Delete upload files
        /// </summary>
        /// <param name="fileId">Id of the folder</param>
        public void DeleteFile(Guid? fileId)
        {
            Task.Run(() =>
            {
                try
                {
                    Delete(fileId);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "The deletion failed.");
                }
            });
        }

        /// <summary>
        /// Start a new reporting task.
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <param name="jsonContent">json job content</param>
        public void RunNewTask(Guid taskId, string jsonContent)
        {
            WorkingCount++;
            Task.Run(() =>
            {
                var task = new Task(() => RunTask(taskId, jsonContent));
                task.Start();
            });
        }

        /// <summary>
        /// Get the engine results
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <param name="taskStatus">Select a special status</param>
        /// <returns></returns>
        public List<JobResult> GetTasks(Guid? taskId = null, TaskStatusInfo? taskStatus = null)
        {
            var results = new List<JobResult>();

            try
            {
                var folders = new List<string>();
                if (taskId.HasValue)
                {
                    logger.Debug($"Get the results of the Task {taskId.Value}.");
                    folders.Add(Path.Combine(Options.TempFolder, taskId.Value.ToString()));
                }
                else
                {
                    logger.Debug($"Get the result of all tasks.");
                    folders.AddRange(Directory.GetDirectories(Options.TempFolder, "*.*", SearchOption.TopDirectoryOnly));
                }

                foreach (var folder in folders)
                {
                    logger.Trace($"Job result \"{folder}\".");
                    var para = GetJobParameter(folder);
                    if (taskStatus.HasValue)
                        results.AddRange(ReportingTask.GetAllResultsFromJob(para).Where(t => t.Status == taskStatus.Value));
                    else
                        results.AddRange(ReportingTask.GetAllResultsFromJob(para));
                }
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task result could not found.");
                return results;
            }
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <returns>Get status</returns>
        public bool StopTasks(Guid? taskId = null)
        {
            if (taskId.HasValue)
            {
                var manager = managerPool[taskId.Value];
                if (manager != null)
                {
                    var task = manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(taskId.ToString())) ?? null;
                    if (task != null)
                    {
                        manager?.Stop();
                        logger.Debug($"The task {taskId.Value} was stopped.");
                        return true;
                    }
                    else
                        logger.Warn($"The task {taskId.Value} was not found.");
                }
                else
                    logger.Debug($"No job manager with id {taskId.Value} found.");
            }
            else
            {
                StopAllJobs();
                WorkingCount = 0;
                logger.Debug($"All tasks was stopped.");
                return true;
            }

            return false;
        }
        #endregion
    }

    /// <summary>
    /// Reporting options
    /// </summary>
    public class ReportingServiceOptions
    {
        #region Properties
        /// <summary>
        /// temp folder for reporting
        /// </summary>
        public string TempFolder { get; set; }
        #endregion
    }
}
