namespace Ser.Engine.Rest.Model
{
    #region Usings
    using Ser.Api;
    using System;
    using System.Threading;
    #endregion

    /// <summary>
    /// Task infromation for task control
    /// </summary>
    public class RestTask
    {
        /// <summary>
        /// Status for Conncetor
        /// </summary>
        public RestTaskStatus TaskStatus { get; set; }

        /// <summary>
        /// Stop Task
        /// </summary>
        public CancellationTokenSource TokenSource { get; set; }

        /// <summary>
        /// Starttime for cleanup
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// EndTime for cleanup
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Remove task by cleanup
        /// </summary>
        public bool Remove { get; set; }
    }
}