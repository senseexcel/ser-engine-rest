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
    #endregion

    public class SerRestService
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properites && Variables
        private JobManager manager = null;
        private ServiceParameter config = null;
        #endregion

        #region Constructor
        public SerRestService(ServiceParameter appConfig)
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

        private string UploadFile(Guid uploadId, ServiceRequestArgs args)
        {
            try
            {
                Task.Run(() =>
                {
                    var uploadFolder = Path.Combine(config.TempDir, uploadId.ToString());
                    Directory.CreateDirectory(uploadFolder);
                    var fullname = Path.Combine(uploadFolder, args.Filename);
                    File.WriteAllBytes(fullname, args.Data);
                    if (args.Unzip)
                        ZipFile.ExtractToDirectory(fullname, uploadFolder, true);
                    logger.Debug($"Upload {uploadId} successfully.");
                });
                return uploadId.ToString();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The file {args?.Filename} with id {uploadId} could not upload.");
                return null;
            }
        }

        private string Delete(string delfolder, Guid? id = null)
        {
            try
            {
                if (id != null && id.HasValue)
                {
                    var deleteFolder = Path.Combine(delfolder, id.ToString());
                    if (Directory.Exists(deleteFolder))
                    {
                        logger.Debug($"Delete folder {deleteFolder}");
                        Directory.Delete(deleteFolder, true);
                    }
                    else
                        logger.Debug($"Delete folder {deleteFolder} not exists.");
                }
                else
                {
                    var folders = Directory.GetDirectories(delfolder, "*.*", SearchOption.TopDirectoryOnly);
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
                }
                logger.Debug("The deletion was completed.");
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The deletion failed.");
                return null;
            }
        }
        #endregion

        #region Public Methods
        public static string GetRequestTextData(HttpListenerRequest request)
        {
            try
            {
                if (!request.HasEntityBody)
                    return null;

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
                        JArray guidArray = jObject?.uploadGuids ?? null;
                        if (guidArray != null)
                        {
                            foreach (var item in guidArray)
                            {
                                var uploadFolder = Path.Combine(config.TempDir, item.Value<string>());
                                logger.Debug($"Copy file from {uploadFolder} to {tempFolder}.");
                                CopyFiles(uploadFolder, tempFolder);
                            }
                        }
                        logger.Debug($"The Task {taskId} was started.");
                        manager = CreateManager(tempFolder, true);
                        manager.Run();
                        logger.Debug($"The Task {taskId} was finished.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"The task {taskId} failed.");
                    }
                });
                return taskId;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task could not created.");
                return null;
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
                });
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The engine could not be stopped properly.");
                return "ERROR";
            }
        }

        public string GetTaskResults(Guid? taskId = null)
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

                //Nur Datei über ID zurück geben


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

        public string PostUploadFile(ServiceRequestArgs args)
        {
            var newGuid = Guid.NewGuid();
            try
            {
                if (args.Id.HasValue)
                {
                    logger.Debug($"Guid {args.Id} was found.");
                    newGuid = args.Id.Value;
                }
                if (args.Data == null)
                    throw new Exception("No content to Upload.");
                var resultId = UploadFile(newGuid, args);
                if (!String.IsNullOrEmpty(resultId))
                {
                    logger.Debug($"Upload {resultId} was started.");
                    return JsonConvert.SerializeObject(resultId);
                }
                return null;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The upload {newGuid} was failed.");
                return null;
            }
        }

        public byte[] GetUploadFile(ServiceRequestArgs args)
        {
            try
            {
                if(!args.Id.HasValue)
                    throw new Exception("Upload id required. - No id");

                //single file
                var uploadPath = String.Empty;
                if (!String.IsNullOrEmpty(args.Filename))
                {
                    uploadPath = Path.Combine(config.TempDir, args.Id.Value.ToString(), args.Filename);
                    if (File.Exists(uploadPath))
                    {
                        logger.Debug($"Find file {uploadPath}");
                        return File.ReadAllBytes(uploadPath);
                    }
                }

                //all files
                uploadPath = Path.Combine(config.TempDir, args.Id.Value.ToString());
                var zipPath = Path.Combine(config.TempDir, $"{Guid.NewGuid().ToString()}.zip");
                logger.Debug($"Create zip file {zipPath}");
                var zipStream = new FileStream(zipPath, FileMode.Create);
                var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);
                var files = Directory.GetFiles(uploadPath);
                foreach (var file in files)
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                archive.Dispose();
                zipStream.Close();
                zipStream.Dispose();
                var zipData = File.ReadAllBytes(zipPath);
                logger.Debug($"File Size: {zipData.Length}");
                logger.Debug($"Remove zip file {zipPath}");
                File.Delete(zipPath);
                logger.Debug("Finish to send.");
                return zipData;
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                return null;
            }
        }

        public string DeleteUpload(ServiceRequestArgs args = null)
        {
            try
            {
                return Delete(config.TempDir, args?.Id);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The deletion failed.");
                return null;
            }
        }
        #endregion
    }
}