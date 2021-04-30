namespace Ser.Engine.Rest.Controllers
{
    #region Usings
    using System;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;
    using NLog;
    using Ser.Api;
    using Ser.Engine.Rest.Services;
    using System.Collections.Generic;
    #endregion

    /// <summary>
    /// Task Operations
    /// </summary>
    public class ReportingOperationsController : Controller
    {
        #region Logger
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties
        private IReportingService Service { get; set; }
        #endregion

        #region Constructor
        /// <summary>
        /// Controller for task operations
        /// </summary>
        /// <param name="service">Reporting service</param>
        public ReportingOperationsController(IReportingService service)
        {
            Service = service ?? throw new Exception("The reporting service was not initialized.");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Create a reporting task.
        /// </summary>
        /// <param name="taskConfig">The JSON job file for the engine.</param>
        /// <param name="taskId">The Id for the task.</param>
        /// <response code="200">Returns a new generated task id.</response>
        [HttpPost]
        [Route("/task/{taskId}")]
        [Produces("application/json", Type = typeof(Guid))]
        public IActionResult RunTasksWithId([FromBody][Required] object taskConfig, [FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Debug($"Create task with Json '{taskConfig?.ToString()}'...");
                var result = Service.RunTask(taskConfig.ToString(), taskId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(RunTasksWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Create a reporting task.
        /// </summary>
        /// <param name="taskConfig">The JSON job file for the engine.</param>
        /// <response code="200">Returns a new generated task id.</response>
        [HttpPost]
        [Route("/task")]
        [Produces("application/json", Type = typeof(Guid))]
        public IActionResult RunTasks([FromBody][Required] object taskConfig)
        {
            try
            {
                logger.Debug($"Create task with Json '{taskConfig?.ToString()}'...");
                var result = Service.RunTask(taskConfig.ToString(), Guid.NewGuid());
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(RunTasks)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Stop the current task.
        /// </summary>
        /// <param name="taskId">The task ID to be deleted.</param>
        /// <response code="200">Get a status.</response>
        [HttpDelete]
        [Route("/task/{taskId}")]
        public IActionResult StopTasksWithId([FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Trace($"Stop task with id '{taskId}'...");
                Service.StopTasks(taskId);
                return Ok();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(StopTasksWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Stop all tasks.
        /// </summary>
        /// <response code="200">Get a status.</response>
        [HttpDelete]
        [Route("/task")]
        public IActionResult StopTasks()
        {
            try
            {
                logger.Trace($"Stop all tasks...");
                Service.StopTasks();
                return Ok();
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(StopTasks)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Gets all task status.
        /// </summary>
        /// <response code="200">Gets the result from the current Task.</response>
        [HttpGet]
        [Route("/status/all")]
        [Produces("application/json", Type = typeof(string))]
        public IActionResult AllStatus()
        {
            try
            {
                logger.Trace($"Get all status...");
                var results = Service.GetAllTaskStatus();
                return Ok(results);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(StatusWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Gets the result from the current Task.
        /// </summary>
        /// <param name="taskId">The task id from which I want to get the result.</param>
        /// <response code="200">Gets the result from the current Task.</response>
        [HttpGet]
        [Route("/status/{taskId}")]
        [Produces("application/json", Type = typeof(string))]
        public IActionResult StatusWithId([FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Trace($"Start get task - Id: {taskId}");
                var result = Service.GetTaskStatus(taskId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(StatusWithId)}' failed.");
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Checks if the reporting service is still responsive.
        /// </summary>
        /// <response code="200">Gets the health status from the task.</response>
        [HttpGet]
        [Route("/health")]
        [Produces("text/plain", Type = typeof(string))]
        public IActionResult HealthStatus()
        {
            try
            {
                logger.Trace($"Start health status");
                var result = Service.HealthStatus();
                return Ok(result);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"The request method '{nameof(HealthStatus)}' failed.");
                return BadRequest(ex.Message);
            }
        }
        #endregion
    }
}