namespace Ser.Engine.Rest.Controllers
{
    #region Usings
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;
    using NLog;
    using System.IO;
    using Microsoft.Extensions.Hosting;
    using Ser.Engine.Rest.Services;
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
        /// Upload a file to the service with a fixed file id.
        /// </summary>
        /// <param name="filename">The Name of the file</param>
        /// <param name="fileId">The file id for the created folder.</param>
        /// <param name="unzip">Use zip files for unpacking after upload.</param>
        /// <response code="200">Returns the transfered file id.</response>
        [HttpPost]
        [Route("/upload/{fileId}")]
        [Consumes("application/octet-stream")]
        [Produces("application/octet-stream")]
        public IActionResult UploadWithId([FromRoute][Required] Guid fileId, [FromHeader][Required] string filename, [FromHeader] bool unzip = false)
        {
            try
            {
                logger.Debug($"Start upload file with Id: '{fileId}'...");
                var result = Service.Upload(fileId, Request.Body, filename, unzip);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(UploadWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Upload a file to the service.
        /// </summary>
        /// <param name="filename">The Name of the file</param>
        /// <param name="unzip">Unpacking zip files after upload.</param>
        /// <response code="200">Returns a new generated file id.</response>
        [HttpPost]
        [Route("/upload")]
        [Consumes("application/octet-stream")]
        [Produces("application/octet-stream")]
        public IActionResult Upload([FromHeader][Required] string filename, [FromHeader] bool unzip = false)
        {
            try
            {
                logger.Debug($"Start upload file...");
                var result = Service.Upload(Guid.NewGuid(), Request.Body, filename, unzip);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(Upload)}' failed.");
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