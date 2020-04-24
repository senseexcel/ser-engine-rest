namespace Ser.Engine.Rest.Controllers
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;
    using Ser.Engine.Rest.Attributes;
    using Swashbuckle.AspNetCore.Annotations;
    using Microsoft.Extensions.Configuration;
    using NLog;
    using Ser.Api;
    using Microsoft.Extensions.Hosting;
    #endregion

    /// <summary>
    /// Controller for task operations
    /// </summary>
    public class TaskOperationsApiController : SerController
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Constructor
        /// <summary>
        /// Controller for task operations
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="service">Reporting service</param>
        public TaskOperationsApiController(IConfiguration configuration, IHostedService service) : base(configuration, service) { }
        #endregion

        #region Public Methods
        /// <summary>
        /// Create a reporting task.
        /// </summary>
        /// <param name="jsonJob">The JSON job file for the engine.</param>
        /// <param name="taskId">The Id for the task.</param>
        /// <response code="200">Returns a new generated task id.</response>
        [HttpPost]
        [Route("/api/v1/task/{taskId}")]
        [ValidateModelState]
        [Consumes("application/json")]
        [Produces("application/json")]
        [SwaggerOperation("CreateTaskWithId", "Create a reporting task.", Tags = new[] { "Create Task" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns a new generated file id.")]
        public virtual IActionResult CreateTaskWithId([FromBody][Required] string jsonJob, [FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Trace($"Start create task - Json: {jsonJob}");
                var result = PostStartTask(jsonJob, taskId);
                return GetRequestAndLog(nameof(CreateTaskWithId), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(CreateTaskWithId)} failed.");
            }
        }

        /// <summary>
        /// Create a reporting task.
        /// </summary>
        /// <param name="jsonJob">The JSON job file for the engine.</param>
        /// <response code="200">Returns a new generated task id.</response>
        [HttpPost]
        [Route("/api/v1/task")]
        [ValidateModelState]
        [Consumes("application/json")]
        [Produces("application/json")]
        [SwaggerOperation("CreateTask", "Create a reporting task.", Tags = new[] { "Create Task" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Returns a new generated file id.")]
        public virtual IActionResult CreateTask([FromBody][Required] string jsonJob)
        {
            try
            {
                logger.Trace($"Start create task - Json: {jsonJob}");
                var result = PostStartTask(jsonJob);
                return GetRequestAndLog(nameof(CreateTask), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(CreateTask)} failed.");
            }
        }

        /// <summary>
        /// Stop all tasks.
        /// </summary>
        /// <response code="200">Get a status.</response>
        [HttpDelete]
        [Route("/api/v1/task")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("StopAllTasks", "Stop all tasks.", Tags = new[] { "Stop Tasks" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Get a status.")]
        public virtual IActionResult StopAllTasks()
        {
            try
            {
                logger.Trace($"Start stop all task");
                var result = DeleteTasks();
                return GetRequestAndLog(nameof(StopAllTasks), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(StopAllTasks)} failed.");
            }
        }

        /// <summary>
        /// Stop the current task.
        /// </summary>
        /// <param name="taskId">The task ID to be deleted.</param>
        /// <response code="200">Get a status.</response>
        [HttpDelete]
        [Route("/api/v1/task/{taskId}")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("StopTasks", "Stop the current task.", Tags = new[] { "Stop Tasks" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Get a status.")]
        public virtual IActionResult StopTasks([FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Trace($"Start delete task - Id: {taskId}");
                var result = DeleteTasks(taskId);
                return GetRequestAndLog(nameof(StopTasks), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(StopTasks)} failed.");
            }
        }

        /// <summary>
        /// Gets the results from all Tasks.
        /// </summary>
        /// <param name="taskStatus">Get all tasks with this status.</param>
        /// <response code="200">Gets the results from all Tasks.</response>
        [HttpGet]
        [Route("/api/v1/task")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("Tasks", "Gets the results from all Tasks.", Tags = new[] { "Task Status" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Gets the results from all Tasks.")]
        public virtual IActionResult Tasks([FromHeader] string taskStatus)
        {
            try
            {
                logger.Trace($"Start get all task.");
                TaskStatusInfo? taskStatusInfo = null;
                if (!String.IsNullOrEmpty(taskStatus))
                    taskStatusInfo = (TaskStatusInfo)Enum.Parse(typeof(TaskStatusInfo), taskStatus, true);
                var result = GetTasks(null, taskStatusInfo);
                return GetRequestAndLog(nameof(Tasks), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(Tasks)} failed.", new OperationResult() { Success = false, Results = new List<JobResult>() });
            }
        }

        /// <summary>
        /// Gets the result from the current Task.
        /// </summary>
        /// <param name="taskId">The task id from which I want to get the result.</param>
        /// <response code="200">Gets the result from the current Task.</response>
        [HttpGet]
        [Route("/api/v1/task/{taskId}")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("TaskWithId", "Gets the result from the current Task.", Tags = new[] { "Task Status" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Gets the result from the current Task.")]
        public virtual IActionResult TaskWithId([FromRoute][Required] Guid taskId)
        {
            try
            {
                logger.Trace($"Start get task - Id: {taskId}");
                var result = GetTasks(taskId);
                return GetRequestAndLog(nameof(TaskWithId), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(TaskWithId)} failed.", new OperationResult() { Success = false, Results = new List<JobResult>() });
            }
        }

        /// <summary>
        /// Check if the task is still alive.
        /// </summary>
        /// <response code="200">Gets the health status from the task.</response>
        [HttpGet]
        [Route("api/v1/task/health")]
        [ValidateModelState]
        [Produces("application/json")]
        [SwaggerOperation("HealthStatus", "Check if the task is still alive", Tags = new[] { "Service Health" })]
        [SwaggerResponse(statusCode: 200, type: typeof(OperationResult), description: "Gets the health status from the task.")]
        public virtual IActionResult HealthStatus()
        {
            try
            {
                logger.Trace($"Start health status");
                var result = GetHealthStatus();
                return GetRequestAndLog(nameof(HealthStatus), result);
            }
            catch (Exception ex)
            {
                return GetBadRequestAndLog(ex, $"The Method {nameof(HealthStatus)} failed.");
            }
        }
        #endregion
    }
}