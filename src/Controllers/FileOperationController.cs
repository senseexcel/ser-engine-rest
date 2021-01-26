namespace Ser.Engine.Rest.Controllers
{
    #region Usings
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;
    using NLog;
    using System.IO;
    using Ser.Engine.Rest.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;
    #endregion

    /// <summary>
    /// File Operations
    /// </summary>
    public class FileOperationsController : Controller
    {
        #region Logger
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private IFileHostingService Service { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Construtor with configuration
        /// </summary>
        /// <param name="service">Reporting service</param>
        public FileOperationsController(IFileHostingService service)
        {
            Service = service ?? throw new Exception("The file hosting service was not initialized.");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Upload a file to the service.
        /// </summary>
        /// <param name="file">The uploaded file (max. 250 MB)</param>
        /// <param name="filename">Individual file name</param>
        /// <response code="200">Returns a new generated file id.</response>
        [HttpPost]
        [Route("/upload")]
        [Consumes("multipart/form-data")]
        [Produces("application/json", Type = typeof(Guid))]
        [RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
        [RequestSizeLimit(262144000)]
        public IActionResult Upload(IFormFile file, [FromHeader] string filename)
        {
            try
            {
                logger.Debug($"Start upload file...");
                var result = Service.Upload(Guid.NewGuid(), file, filename);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(Upload)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Upload a file to the service with a fixed file id.
        /// </summary>
        /// <param name="file">The uploaded file (max. 250 MB)</param>
        /// <param name="fileId">The file id for the created folder.</param>
        /// <param name="filename">Individual file name</param>
        /// <response code="200">Returns the transfered file id.</response>
        [HttpPost]
        [Route("/upload/{fileId}")]
        [Consumes("multipart/form-data")]
        [Produces("application/json", Type = typeof(Guid))]
        [RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
        [RequestSizeLimit(262144000)]
        public IActionResult UploadWithId(IFormFile file, [FromRoute][Required] Guid fileId, [FromHeader] string filename)
        {
            try
            {
                logger.Debug($"Start upload file with Id: '{fileId}'...");
                var result = Service.Upload(fileId, file, filename);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(UploadWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get the files from the service.
        /// </summary>
        /// <param name="folderId">The folder id that has been created.</param>
        /// <param name="filename">Special file to be returned.</param>
        /// <response code="200">Returns the file from id.</response>
        [HttpGet]
        [Route("/download/{folderId}")]
        [Produces("application/octet-stream", Type = typeof(File))]
        public IActionResult Download([FromRoute][Required] Guid folderId, [FromHeader] string filename)
        {
            try
            {
                logger.Debug($"Start download - Id: '{folderId}' and Filename: '{filename}'...");
                var fileData = Service.Download(folderId, filename);
                logger.Trace($"Response file data with length with '{fileData?.Length}'...");
                return File(fileData, "application/octet-stream", "download.zip");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(Download)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Delete a file in the service.
        /// </summary>
        /// <param name="folderId">Special folder to be deleted.</param>
        /// <response code="200">Returns the operation status.</response>
        [HttpDelete]
        [Route("/delete/{folderId}")]
        public IActionResult DeleteWithId([FromRoute][Required] Guid folderId)
        {
            try
            {
                logger.Debug($"Delete folder with id '{folderId}'...");
                Service.Delete(folderId);
                return Ok();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(DeleteWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Delete files in the service.
        /// </summary>
        /// <response code="200">Returns the operation status.</response>
        [HttpDelete]
        [Route("/delete")]
        public IActionResult Delete()
        {
            try
            {
                logger.Debug($"Delete all files...");
                Service.Delete();
                return Ok();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(Delete)}' failed.");
                return BadRequest(ex.Message);
            }
        }
        #endregion
    }
}