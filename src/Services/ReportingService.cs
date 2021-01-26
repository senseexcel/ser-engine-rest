namespace Ser.Engine.Rest.Services
{
    #region Usings
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using NLog;
    using Prometheus;
    using Ser.Api;
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
        /// <param name="taskConfig">Job file as json</param>
        /// <param name="taskId">Task id</param>
        /// <returns>Return id of the task</returns>
        public Guid RunTask(object taskConfig, Guid taskId);

        /// <summary>
        /// Get Status from Task(s)
        /// </summary>
        /// <param name="taskId">Task id</param>
        /// <param name="taskStatus">Task status</param>
        /// <returns>Return job results</returns>
        public List<JobResult> GetTasks(Guid? taskId = null, TaskStatusInfo? taskStatus = null);

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
        private readonly ConcurrentDictionary<Guid, JobManager> managerPool;
        private readonly Counter taskCounter = null;
        private readonly object threadlock = new object();

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

        private static JobManager CreateManager(string workdir)
        {
            return new JobManager(GetJobParameter(workdir));
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
                    System.IO.File.Copy(copyFile, destFile, true);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Copy Progress from {sourceFolder} to {targetFolder} failed.");
            }
        }

        private void StopAllJobs()
        {
            logger.Debug("Stop all jobs.");
            foreach (var manager in managerPool)
                manager.Value?.Stop();
            WorkingCount = 0;
            taskCounter.Inc(WorkingCount);
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Start a new reporting task.
        /// </summary>
        /// <param name="taskConfig">json job content</param>
        /// <param name="taskId">Id of the task</param>
        public Guid RunTask(object taskConfig, Guid taskId)
        {
            WorkingCount++;
            taskCounter.Inc(WorkingCount);
            Task.Run(() =>
            {
                try
                {
                    logger.Debug($"START - {taskId}");
                    var taskFolder = Path.Combine(Options.TempFolder, taskId.ToString());
                    Directory.CreateDirectory(taskFolder);
                    var jObject = JObject.FromObject(taskConfig) as dynamic;
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
                    manager.Run(taskConfig?.ToString(), taskId.ToString());
                    logger.Debug($"The Task {taskId} was finished.");
                    managerPool.TryRemove(taskId, out manager);
                    WorkingCount--;
                    taskCounter.Inc(WorkingCount);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"The task {taskId} failed.");
                }
            });
            return taskId;
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
        public void StopTasks(Guid? taskId = null)
        {
            if (taskId.HasValue)
            {
                var key = managerPool?.Keys?.ToList()?.FirstOrDefault(t => t.ToString() == taskId?.ToString()) ?? new Guid();
                if (key == Guid.Empty)
                    return;

                var manager = managerPool[key];
                if (manager != null)
                {
                    var task = manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(taskId.ToString())) ?? null;
                    if (task != null)
                    {
                        manager?.Stop();
                        logger.Debug($"The task {taskId.Value} was stopped.");
                        return;
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
                return;
            }
        }

        /// <summary>
        /// Return the reporting service health.
        /// </summary>
        /// <returns>health status</returns>
        public string HealthStatus()
        {
            var status = "ready";
            if (WorkingCount > 0)
                status = "running";
            return status;
        }
        #endregion
    }
}