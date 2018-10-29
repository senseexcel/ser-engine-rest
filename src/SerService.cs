namespace Ser.Engine.Rest
{
    #region Usings
    using NLog;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SerEngine;
    using SerEngine.Jobs;
    using System.IO;
    using Newtonsoft.Json.Linq;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using System.Web;
    using System.Net;
    using Newtonsoft.Json;
    using System.Threading;
    using Ser.Api;
    #endregion

    public class SerService
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properites && Variables
        private JobManager manager = null;
        private ServiceParameter config = null;
        #endregion

        #region Constructor
        public SerService(ServiceParameter appConfig)
        {
            config = appConfig;
        }
        #endregion

        #region Private Methods
        private void CopyFiles(string sourceFolder, string targetFolder)
        {
            var files = Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                var destPath = Path.Combine(targetFolder, Path.GetFileName(file));
                File.Copy(file, destPath, true);
            }
        }

        private AppParameter GetJobParameter(string workdir)
        {
            var args = new string[] { $"--workdir={workdir}" };
            return new AppParameter(args);
        }

        private JobManager CreateManager(string workdir, bool createNew = false)
        {
            if (manager != null && !createNew)
                return manager;
            return new JobManager(GetJobParameter(workdir));
        }

        private string UploadFile(byte[] fileData, Guid uploadId, ServiceRequestArgs args)
        {
            try
            {
                Task.Run(() =>
                {
                    var uploadFolder = Path.Combine(config.TempDir, uploadId.ToString());
                    Directory.CreateDirectory(uploadFolder);
                    var fullname = Path.Combine(uploadFolder, args.Filename);
                    File.WriteAllBytes(fullname, fileData);

                    if (args.Unzip)
                        ZipFile.ExtractToDirectory(fullname, uploadFolder);
                });
                return uploadId.ToString();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The file {args?.Filename} could not upload.");
                return "ERROR";
            }
        }

        private string Delete(string delfolder, Guid? id = null)
        {
            try
            {
                if (id != null && id.HasValue)
                {
                    var deleteFolder = Path.Combine(delfolder, id.ToString());
                    Directory.Delete(deleteFolder, true);
                }
                else
                {
                    var folders = Directory.GetDirectories(delfolder, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var folder in folders)
                    {
                        try
                        {
                            Directory.Delete(folder, true);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"The folder {folder} could not delete.");
                        }
                    }
                }
                logger.Debug("The delete was completed.");
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The delete failed.");
                return "ERROR";
            }
        }
        #endregion

        #region Public Methods
        public string CreateTask(string jsonRequest)
        {
            try
            {
                logger.Trace($"JSONREQUEST: {jsonRequest}");
                var taskId = Guid.NewGuid().ToString();
                Task.Run(() =>
                {
                    try
                    {
                        var tempFolder = Path.Combine(config.TempDir, taskId);
                        Directory.CreateDirectory(tempFolder);
                        File.WriteAllText(Path.Combine(tempFolder, "job.json"), jsonRequest, Encoding.UTF8);
                        var jObject = JObject.Parse(jsonRequest) as dynamic;
                        JArray guidArray = jObject.uploadGuids;
                        foreach (var item in guidArray)
                        {
                            var uploadFolder = Path.Combine(config.TempDir, item.Value<string>());
                            logger.Debug($"Copy file from {uploadFolder} to {tempFolder}.");
                            CopyFiles(uploadFolder, tempFolder);
                        }
                        manager = CreateManager(tempFolder, true);
                        manager.Run();
                        logger.Debug($"The Task {taskId} was finished.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "The task start failed.");
                    }
                });
                return taskId;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task could not created.");
                return "ERROR";
            }
        }

        public string StopTasks(Guid? taskId = null)
        {
            try
            {
                Task.Run(() =>
                {
                    if (taskId != null && taskId.HasValue)
                    {
                        var tempFolder = Path.Combine(config.TempDir, taskId.Value.ToString());
                        manager = CreateManager(tempFolder);
                        manager.Load();
                        var task = manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(taskId.ToString())) ?? null;
                        if (task != null)
                        {
                            manager?.Stop(task.Id);
                            Thread.Sleep(1000);
                            logger.Debug($"The Task {taskId.Value} was stopped.");
                        }
                        else
                            logger.Warn($"The Task {taskId.Value} was not found.");
                    }
                    else
                    {
                        manager?.Stop();
                        Thread.Sleep(1000);
                    }
                    return Delete(config.TempDir, taskId);
                });
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The engine could not be stopped properly.");
                return "ERROR";
            }
        }

        public string GetTaskResult(Guid? taskId = null)
        {
            try
            {
                if (!taskId.HasValue)
                {
                    logger.Debug($"No Task id in the request.");
                    return null;
                }

                logger.Debug($"Get the result of the Task {taskId.Value}.");
                var tempFolder = Path.Combine(config.TempDir, taskId.Value.ToString());
                if (manager == null)
                {
                    manager = CreateManager(tempFolder);
                    manager.Load();
                }
                ZipArchive archive = null;
                FileStream zipStream = null;
                string zipPath = null;
                var task = manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(taskId.ToString())) ?? null;
                if (task != null && task?.Status == TaskStatusInfo.SUCCESS)
                {
                    foreach (var result in task.Results)
                    {
                        foreach (var report in result.Reports)
                        {
                            if (report.Paths.Count == 1)
                            {
                                var path = report.Paths.FirstOrDefault() ?? null;
                                if (File.Exists(path))
                                    return path;
                            }
                            else
                            {
                                if (archive == null)
                                {
                                    zipPath = Path.Combine(task.JobParameters.WorkDir, "reports.zip");
                                    zipStream = new FileStream(zipPath, FileMode.Create);
                                    archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
                                }
                                foreach (var path in report.Paths)
                                    archive.CreateEntryFromFile(path, Path.GetFileName(path));
                            }
                        }
                    }

                    archive.Dispose();
                    zipStream.Close();
                    zipStream.Dispose();
                    return zipPath;
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task result could not found.");
                return null;
            }
        }

        public List<JobResult> GetTasks(Guid? taskId = null)
        {
            try
            {
                if (taskId != null && taskId.HasValue)
                {
                    var tempFolder = Path.Combine(config.TempDir, taskId.Value.ToString());
                    manager = CreateManager(tempFolder);
                    manager.Load();
                    logger.Debug($"Get the result of the Task {taskId.Value}.");
                    return manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(taskId.ToString())).Results ?? new List<JobResult>();
                }
                else
                {
                    logger.Debug($"Get the result of all tasks.");
                    var results = new List<JobResult>();
                    var folders = Directory.GetDirectories(config.TempDir, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var folder in folders)
                    {
                        var para = GetJobParameter(folder);
                        results.AddRange(ReportingTask.GetAllResultsFromJob(para));
                    }
                    return results;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task result could not found.");
                return new List<JobResult>();
            }
        }

        public string UploadFile(byte[] fileData, ServiceRequestArgs args)
        {
            try
            {
                var newGuid = Guid.NewGuid();
                if (args.Id.HasValue)
                {
                    logger.Debug($"Guid {args.Id} was found.");
                    newGuid = args.Id.Value;
                }
                if (String.IsNullOrEmpty(args.Filename))
                {
                    logger.Error("A filename is requiered.");
                    return "A filename is required";
                }
                var results = UploadFile(fileData, newGuid, args);
                logger.Debug("Upload successfully");
                return JsonConvert.SerializeObject(results);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The upload was failed.");
                return "ERROR";
            }
        }

        public string DeleteUploads(ServiceRequestArgs args = null)
        {
            try
            {
                return Delete(config.TempDir, args.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The delete failed.");
                return "ERROR";
            }
        }

        public string GetUploadIds(Guid? uploadId = null)
        {
            try
            {
                var results = new List<string>();
                if (uploadId != null && uploadId.HasValue)
                {
                    var folder = Path.Combine(config.TempDir, uploadId.ToString());
                    if (Directory.Exists(folder))
                    {
                        results.Add(uploadId.ToString());
                        return JsonConvert.SerializeObject(results);
                    }
                    logger.Debug($"No folder {folder} found.");
                }
                var folders = Directory.GetDirectories(config.TempDir, "*.*", SearchOption.TopDirectoryOnly);
                if(folders.Length == 0)
                    logger.Debug($"No folder found.");
                foreach (var folder in folders)
                    results.Add(folder.Split(new string[] { "\\", "/" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault());
                return JsonConvert.SerializeObject(results);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The files could not be determined.");
                return "ERROR";
            }
        }
        #endregion
    }
}