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
        private static Dictionary<Guid, JobManager> managers = null;
        private static ServiceParameter config = null;
        #endregion

        #region Constructor
        public SerRestService(ServiceParameter appConfig)
        {
            config = appConfig;
            managers = new Dictionary<Guid, JobManager>();
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

        private JobManager CreateManager(string workdir)
        {
            return new JobManager(GetJobParameter(workdir));
        }

        private bool UploadFile(Guid uploadId, ServiceRequestArgs args)
        {
            try
            {
                var uploadFolder = Path.Combine(config.TempDir, uploadId.ToString());
                Directory.CreateDirectory(uploadFolder);
                var fullname = Path.Combine(uploadFolder, args.Filename);
                File.WriteAllBytes(fullname, args.PostData);
                if (args.Unzip)
                {
                    logger.Debug($"Unzip file {fullname}");
                    ZipFile.ExtractToDirectory(fullname, uploadFolder, true);
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The file {args?.Filename} with id {uploadId} could not upload.");
                return false;
            }
        }

        private void Delete(Guid? id = null)
        {
            try
            {
                if (id != null && id.HasValue)
                {
                    var deleteFolder = Path.Combine(config.TempDir, id.ToString());
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
                    var folders = Directory.GetDirectories(config.TempDir, "*.*", SearchOption.TopDirectoryOnly);
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
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The deletion failed.");
            }
        }

        private bool CheckId(ServiceRequestArgs args)
        {
            if (args != null && args?.Id == null)
            {
                logger.Warn("A Id is required. No Id found.");
                return false;
            }
            return true;
        }
        #endregion

        #region Public File Methods
        public string PostUploadFile(ServiceRequestArgs args)
        {
            var newGuid = Guid.NewGuid();
            try
            {
                if (args?.Id != null)
                {
                    logger.Debug($"Guid {args?.Id} was found.");
                    newGuid = args.Id.Value;
                }
                Task.Run(() =>
                {
                    if(String.IsNullOrEmpty(args?.Filename))
                    {
                        logger.Error("No Filename found.");
                        return;
                    }
                    if (args?.PostData == null)
                    {
                        logger.Error("No content to Upload.");
                        return;
                    }
                    var result = UploadFile(newGuid, args);
                    if (result)
                        logger.Debug($"Upload {newGuid} successfully.");
                    else
                        logger.Debug($"Upload {newGuid} was failed.");
                });
                return newGuid.ToString();
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
                if (!args.Id.HasValue)
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
                logger.Info($"Create zip file \"{zipPath}\"");
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
                logger.Debug($"Remove zip file \"{zipPath}\"");
                File.Delete(zipPath);
                logger.Debug("Finish to send.");
                return zipData;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The call failed.");
                return null;
            }
        }

        public string DeleteUpload(ServiceRequestArgs args = null)
        {
            try
            {
                if(!CheckId(args))
                    return null;

                Task.Run(() =>
                {
                    Delete(args?.Id);
                });
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The deletion failed.");
                return null;
            }
        }
        #endregion

        #region Public Task Methods
        public string CreateTask(ServiceRequestArgs args)
        {
            try
            {
                logger.Trace($"JSONREQUEST: {args?.PostText}");
                if(String.IsNullOrEmpty(args?.PostText))
                {
                    logger.Error("The json request was emtpy.");
                    return null;
                }
                var taskId = Guid.NewGuid();
                Task.Run(() =>
                {
                    try
                    {
                        var tempFolder = Path.Combine(config.TempDir, taskId.ToString());
                        Directory.CreateDirectory(tempFolder);
                        File.WriteAllText(Path.Combine(tempFolder, "job.json"), args.PostText, Encoding.UTF8);
                        var jObject = JObject.Parse(args.PostText) as dynamic;
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
                        var manager = CreateManager(tempFolder);
                        managers.Add(taskId, manager);
                        manager.Run();
                        logger.Debug($"The Task {taskId} was finished.");
                        managers.Remove(taskId);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"The task {taskId} failed.");
                    }
                });
                return taskId.ToString();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The task could not created.");
                return null;
            }
        }

        public List<JobResult> GetTasks(ServiceRequestArgs args = null)
        {
            var results = new List<JobResult>();

            try
            {
                if (!CheckId(args))
                    return results;

                if (args != null && args?.Id != null)
                {
                    var taskFolder = Path.Combine(config.TempDir, args.Id.Value.ToString());
                    var para = GetJobParameter(taskFolder);
                    results.AddRange(ReportingTask.GetAllResultsFromJob(para));
                    logger.Debug($"Get the results of the Task {args.Id.Value}.");
                    return results;
                }
                else
                {
                    logger.Debug($"Get the result of all tasks.");
                    var folders = Directory.GetDirectories(config.TempDir, "*.json", SearchOption.TopDirectoryOnly);
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

        public string StopTasks(ServiceRequestArgs args = null)
        {
            try
            {
                if (!CheckId(args))
                    return null;

                Task.Run(() =>
                {
                    if (args != null && args.Id != null)
                    {
                        var tempFolder = Path.Combine(config.TempDir, args.Id.Value.ToString());
                        managers.TryGetValue(args.Id.Value, out var manager);
                        if (manager != null)
                        {
                            manager.Load();
                            var task = manager?.Tasks.FirstOrDefault(t => t.JobParameters.WorkDir.Contains(args.Id.ToString())) ?? null;
                            if (task != null)
                            {
                                manager.Stop(task.Id);
                                logger.Debug($"The task {args.Id.Value} was stopped.");
                            }
                            else
                                logger.Warn($"The task {args.Id.Value} was not found.");
                        }
                        else
                            logger.Debug($"No job manager with id {args.Id.Value} found.");
                    }
                    else
                    {
                        foreach (var manager in managers)
                        {
                            logger.Debug($"The task {manager.Key} was stopped.");
                            manager.Value.Stop();
                        }
                        logger.Debug($"All tasks was stopped.");
                    }
                });
                return "OK";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The engine could not be stopped properly.");
                return null;
            }
        }
        #endregion
    }
}