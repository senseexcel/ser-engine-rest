namespace Ser.Engine.Rest.Services
{
    #region Usings
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Prometheus;
    using Ser.Api;
    using Ser.Distribute;
    using Ser.Engine.Jobs;
    using Ser.Engine.Rest.Model;
    #endregion

    #region Interfaces
    /// <summary>
    /// Reproting service 
    /// </summary>
    public interface IReportingService
    {
        /// <summary>
        /// Run Task(s)
        /// </summary>
        /// <param name="taskConfig">Job file json as string</param>
        /// <param name="taskId">Task id</param>
        /// <returns>Return id of the task</returns>
        public Guid RunTask(string taskConfig, Guid taskId);

        /// <summary>
        /// Get Status from special Task
        /// </summary>
        /// <param name="taskId">Task id</param>
        /// <returns>Return the Task status</returns>
        public RestTaskStatus GetTaskStatus(Guid taskId);

        /// <summary>
        /// Get all status from Tasks
        /// </summary>
        /// <returns>Return a list of all status</returns>
        public List<RestTaskStatus> GetAllTaskStatus();

        /// <summary>
        /// Stop task(s)
        /// </summary>
        /// <param name="taskId">Task id</param>
        public void StopTasks(Guid? taskId = null);

        /// <summary>
        /// Return the reporting service health.
        /// </summary>
        /// <returns>Reporting service health</returns>
        public string HealthStatus();
    }
    #endregion

    /// <summary>
    /// Reporting service
    /// </summary>
    public class ReportingService : IReportingService
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        private readonly ConcurrentDictionary<Guid, RestTask> taskPool;
        private readonly Counter taskCounter = null;
        private int WorkingCount { get; set; }
        private ReportingServiceOptions Options { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initialize reporting service
        /// </summary>
        /// <param name="options">options</param>
        public ReportingService(ReportingServiceOptions options)
        {
            Options = options ?? throw new Exception("No reporting options found.");
            taskPool = new ConcurrentDictionary<Guid, RestTask>();
            WorkingCount = 0;
            taskCounter = Metrics.CreateCounter($"workerGauge", "Number of tasks", new CounterConfiguration()
            {
                LabelNames = new[] { "worker" }
            });
        }
        #endregion

        #region Private Methods
        private static AppParameter GetJobParameter(string workdir)
        {
            var args = new string[] { $"--workdir={workdir}" };
            return new AppParameter(args);
        }

        private static void CopyFiles(string sourceFolder, string targetFolder)
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
                    File.Copy(copyFile, destFile, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Copy Progress from {sourceFolder} to {targetFolder} failed.");
            }
        }

        private List<JobResult> GetResults(Guid taskId, bool readfileData = false)
        {
            var results = new List<JobResult>();

            try
            {
                logger.Debug($"Get the results of the Task {taskId}.");
                var taskFolder = Path.Combine(Options.TempFolder, taskId.ToString());

                logger.Trace($"Job result \"{taskFolder}\".");
                var para = GetJobParameter(taskFolder);
                results.AddRange(ReportingTask.GetAllResultsFromJob(para));

                if (readfileData)
                {
                    foreach (var result in results)
                        foreach (var report in result.Reports)
                            foreach (var path in report.Paths)
                                report.Data.Add(new ReportData() { Filename = Path.GetFileName(path), DownloadData = File.ReadAllBytes(path) });
                }

                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task result could not found.");
                return results;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Start a new reporting task.
        /// </summary>
        /// <param name="taskConfig">Job file json as string</param>
        /// <param name="taskId">Id of the task</param>
        public Guid RunTask(string taskConfig, Guid taskId)
        {
            Task.Run(() =>
            {
                try
                {
                    logger.Debug($"Start rest task with id {taskId}...");
                    WorkingCount++;
                    taskCounter.Inc(WorkingCount);
                    var restStatus = new RestTaskStatus() { Status = 1, ProcessMessage = "Report job will be started...." };
                    var tokenSource = new CancellationTokenSource();
                    taskPool.TryAdd(taskId, new RestTask() { TokenSource = tokenSource, TaskStatus = restStatus, StartTime = DateTime.Now });

                    restStatus.Status = 1;
                    restStatus.ProcessMessage = "Report job is running...";
                    var taskFolder = Path.Combine(Options.TempFolder, taskId.ToString());
                    Directory.CreateDirectory(taskFolder);
                    var jObject = JObject.Parse(taskConfig) as dynamic;
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
                    var manager = new JobManager(GetJobParameter(taskFolder));
                    manager.Run(taskConfig?.ToString(), taskId.ToString(), tokenSource.Token);

                    restStatus.Status = 2;
                    restStatus.ProcessMessage = "Report job is distributed...";
                    var distManager = new DistributeManager();
                    var distResult = distManager.Run(GetResults(taskId, true), tokenSource.Token);
                    if (distResult != null)
                    {
                        logger.Debug($"Distribute result: '{distResult}'");
                        logger.Debug("The delivery was successfully.");
                        restStatus.ProcessMessage = "Report job was successful finish";
                        restStatus.DistributeResult = distResult;
                        restStatus.JobResultJson = JsonConvert.SerializeObject(GetResults(taskId));
                        restStatus.Status = 3;
                    }
                    else
                    {
                        logger.Debug("The delivery was failed.");
                        restStatus.ProcessMessage = "Report job was failed";
                        restStatus.DistributeResult = distManager.ErrorMessage;
                        restStatus.JobResultJson = JsonConvert.SerializeObject(GetResults(taskId));
                        restStatus.Status = -1;
                    }
                    logger.Debug($"Cleanup old rest tasks...");
                    Cleanup();
                    WorkingCount--;
                    taskCounter.Inc(WorkingCount);
                    logger.Debug($"The Task {taskId} was finished.");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"The task {taskId} failed.");
                }
            });
            return taskId;
        }

        /// <summary>
        /// Stop the task
        /// </summary>
        /// <param name="taskId">Id of the task</param>
        /// <returns>Get status</returns>
        public void StopTasks(Guid? taskId = null)
        {
            try
            {
                if (taskId.HasValue)
                {
                    var key = taskPool?.Keys?.ToList()?.FirstOrDefault(t => t.ToString() == taskId?.ToString()) ?? new Guid();
                    if (key == Guid.Empty)
                        return;

                    var restTask = taskPool[key];
                    if (restTask != null)
                    {
                        restTask.TaskStatus.ProcessMessage = "Report job will be stopped...";
                        restTask.TaskStatus.Status = 4;
                        restTask.TokenSource.Cancel();
                        logger.Debug($"The rest task {taskId} was stopped.");
                    }
                    else
                        logger.Debug($"No rest task with id {taskId.Value} found.");
                }
                else
                {
                    logger.Debug("Stop all rest tasks.");
                    foreach (var restTaskValue in taskPool.Values)
                        restTaskValue.TokenSource?.Cancel();
                    WorkingCount = 0;
                    taskCounter.Inc(WorkingCount);
                    logger.Debug($"All rest tasks was stopped.");
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stop rest jobs failed.");
            }
        }

        /// <summary>
        /// Get all rest tasks status
        /// </summary>
        /// <returns></returns>
        public List<RestTaskStatus> GetAllTaskStatus()
        {
            try
            {
                var results = new List<RestTaskStatus>();
                foreach (var value in taskPool.Values)
                    results.Add(value.TaskStatus);
                return results;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Get rest all status failed.");
                return new List<RestTaskStatus>();
            }
        }

        /// <summary>
        /// Get the full status of a reporting task engine + distribute
        /// </summary>
        /// <param name="taskId">Task id</param>
        /// <returns>Status object</returns>
        public RestTaskStatus GetTaskStatus(Guid taskId)
        {
            try
            {
                if (taskPool.TryGetValue(taskId, out var restTask))
                {
                    logger.Debug($"Get rest task status: '{JsonConvert.SerializeObject(restTask?.TaskStatus, Formatting.Indented)}'");
                    return restTask?.TaskStatus;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Get rest status failed.");
                return null;
            }
        }

        /// <summary>
        /// Cleanup Task Status
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (taskPool.IsEmpty)
                    return;

                foreach (var keypair in taskPool.ToList())
                {
                    var restTask = keypair.Value;
                    var span = restTask.StartTime - restTask.EndTime;
                    if (span.TotalSeconds >= 15)
                        taskPool.TryRemove(keypair);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Rest Cleanup failed.");
            }
        }

        /// <summary>
        /// Return the reporting service health.
        /// </summary>
        /// <returns>health status</returns>
        public string HealthStatus()
        {
            var status = "Ready";
            if (WorkingCount > 0)
                status = $"Running with {WorkingCount} task(s)";
            return status;
        }
        #endregion
    }
}