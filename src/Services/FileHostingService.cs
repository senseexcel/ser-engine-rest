namespace Ser.Engine.Rest.Services
{
    using Microsoft.AspNetCore.Http;
    #region Usings
    using NLog;
    using Ser.Engine.Rest.Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Threading.Tasks;
    #endregion

    #region Interfaces
    /// <summary>
    /// File hostring interface for controller
    /// </summary>
    public interface IFileHostingService
    {
        /// <summary>
        /// Upload file
        /// </summary>
        /// <param name="fileId">File id</param>
        /// <param name="fileStream">File stream</param>
        /// <param name="fileName">Individual file name</param>
        /// <returns>The id from the uploded file or folder</returns>
        public Guid Upload(Guid fileId, IFormFile fileStream, string fileName = null);

        /// <summary>
        /// Delete Folder(s)
        /// </summary>
        /// <param name="folderId">Id of the special directory</param>
        public void Delete(Guid? folderId = null);

        /// <summary>
        /// Download File or Folder
        /// </summary>
        /// <param name="folderId">Id of the special directory from which a zip will be created.</param>
        /// <param name="filename">Filename of the single file</param>
        /// <returns>The data as byte array</returns>
        public byte[] Download(Guid? folderId, string filename = null);
    }
    #endregion

    /// <summary>
    /// File hosting service
    /// </summary>
    public class FileHostingService : IFileHostingService
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties && Variables
        /// <summary>
        /// File hosting options
        /// </summary>
        public FileHostingOptions Options { get; private set; }
        private readonly object threadlock = new object();
        #endregion

        #region Constructor
        /// <summary>
        /// Consturktur of the file hosting service
        /// </summary>
        /// <param name="options">File hosting options</param>
        public FileHostingService(FileHostingOptions options)
        {
            Options = options;
        }
        #endregion

        #region Private Methods
        private static byte[] GetBytesFromStream(IFormFile file)
        {
            try
            {
                if (file == null)
                    throw new Exception("The uploaded file is null.");

                var fileLength = Convert.ToInt32(file.Length);
                using var ms = new MemoryStream(fileLength);
                file.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("The stream could not convert to byte array.", ex);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Write uploaded file to folder
        /// </summary>
        /// <param name="fileId">Name of the file</param>
        /// <param name="file">File from upload</param>
        /// <param name="filename">Individual file name</param>
        public Guid Upload(Guid fileId, IFormFile file, string filename = null)
        {
            try
            {
                logger.Debug($"Upload file with the following parameters: ID='{fileId}' Name='{file?.Name}'...");
                var fileData = GetBytesFromStream(file);
                Task.Run(() =>
                {
                    try
                    {
                        var uploadFolder = Path.Combine(Options.TempFolder, fileId.ToString());
                        Directory.CreateDirectory(uploadFolder);
                        var uploadfilename = file?.FileName;
                        if (!String.IsNullOrEmpty(filename))
                            uploadfilename = filename;
                        var fullname = Path.Combine(uploadFolder, uploadfilename);
                        File.WriteAllBytes(fullname, fileData);
                        if (Path.GetExtension(file.FileName) == ".zip")
                        {
                            logger.Debug($"Unzip uploaded file '{fullname}'...");
                            ZipFile.ExtractToDirectory(fullname, uploadFolder, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Upload file with id '{fileId}' failed.");
                    }
                });
                return fileId;
            }
            catch (Exception ex)
            {
                throw new Exception("The file upload could not be initialized.", ex);
            }
        }

        /// <summary>
        /// Delete upload files
        /// </summary>
        /// <param name="folderId">Id of the folder</param>
        public void Delete(Guid? folderId = null)
        {
            Task.Run(() =>
            {
                try
                {
                    var folders = new List<string>();
                    if (folderId.HasValue)
                        folders.Add(Path.Combine(Options.TempFolder, folderId.ToString()));
                    else
                        folders.AddRange(Directory.GetDirectories(Options.TempFolder, "*.*", SearchOption.TopDirectoryOnly));

                    foreach (var folder in folders)
                    {
                        try
                        {
                            logger.Debug($"Delete folder '{folder}'...");
                            lock (threadlock)
                            {
                                if (Directory.Exists(folder))
                                    Directory.Delete(folder, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"The folder {folder} could not delete.");
                        }
                    }

                    logger.Debug("The deletion was completed.");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "The deletion failed.");
                }
            });
        }

        /// <summary>
        /// Get the file from the upload folder
        /// </summary>
        /// <param name="folderId">Id of the folder</param>
        /// <param name="filename">Name of the file</param>
        /// <returns></returns>
        public byte[] Download(Guid? folderId, string filename = null)
        {
            try
            {
                if (!folderId.HasValue)
                    throw new Exception("File id for download required.");

                //return single file
                var uploadPath = String.Empty;
                if (!String.IsNullOrEmpty(filename))
                {
                    uploadPath = Path.Combine(Options.TempFolder, folderId.Value.ToString(), filename);
                    if (File.Exists(uploadPath))
                    {
                        logger.Debug($"Find file '{uploadPath}'...");
                        return File.ReadAllBytes(uploadPath);
                    }
                }

                //zip all files and return
                uploadPath = Path.Combine(Options.TempFolder, folderId.Value.ToString());
                var zipPath = Path.Combine(Options.TempFolder, $"{Guid.NewGuid()}.zip");
                logger.Info($"Create zip file '{zipPath}'");
                ZipFile.CreateFromDirectory(uploadPath, zipPath, CompressionLevel.Fastest, false);
                var zipData = File.ReadAllBytes(zipPath);
                logger.Debug($"File Size: '{zipData.Length}'");
                logger.Debug($"Remove zip file '{zipPath}'");
                File.Delete(zipPath);
                logger.Debug("Finish to send.");
                return zipData;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The folder '{folderId}' could not download.");
                return null;
            }
        }
        #endregion
    }
}