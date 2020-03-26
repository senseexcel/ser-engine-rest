namespace Ser.Engine.Rest.Services
{
    #region Usings
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// Base class for implementing a long running <see cref="IHostedService"/>
    /// </summary>
    public abstract class BackgroundService : IHostedService, IDisposable
    {
        #region Public Methods
        /// <summary>
        /// Start service
        /// </summary>
        /// <param name="cancellationToken">use cancel token</param>
        /// <returns> A Task</returns>
        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop service
        /// </summary>
        /// <param name="cancellationToken">use cancel token</param>
        /// <returns>A task</returns>
        public virtual Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Make free and stop all tasks.
        /// </summary>
        public virtual void Dispose() { }
        #endregion
    }
}