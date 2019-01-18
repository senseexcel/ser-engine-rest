namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NLog;
    #endregion

    public static class AppExit
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        public static void WaitFor(CancellationTokenSource cts, params Task[] tasks)
        {
            if (cts == null)
                throw new ArgumentNullException(nameof(cts));

            if (tasks == null)
                throw new ArgumentNullException(nameof(tasks));

            Task.Run(() =>
            {
                logger.Info("------ Service Wait for Exit ------");
                while (true)
                    Thread.Sleep(500);
            }).Wait();
            CancelTasks(cts);
            WaitTasks(tasks);
        }

        private static void CancelTasks(CancellationTokenSource cts)
        {
            logger.Info("\nWaiting for the tasks to complete...");
            cts.Cancel();
        }

        private static void WaitTasks(Task[] tasks)
        {
            try
            {
                foreach (var t in tasks)
                    t.Wait();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }
    }
}