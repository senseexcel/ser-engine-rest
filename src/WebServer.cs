namespace Ser.Engine.Rest
{
    #region Usings
    using System;
    using System.Net;
    using System.Threading;
    using System.Linq;
    using System.Text;
    #endregion

    public class WebServer
    {
        private readonly HttpListener listener = new HttpListener();
        private readonly Func<HttpListenerRequest, string> responderMethod;

        public WebServer(string[] prefixes, Func<HttpListenerRequest, string> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException("Needs Windows XP SP2, Server 2003 or later.");

            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("no prefixes");

            foreach (string s in prefixes)
                listener.Prefixes.Add(s);

            responderMethod = method ?? throw new ArgumentException("no method");
            listener.Start();
        }

        public WebServer(Func<HttpListenerRequest, string> method, params string[] prefixes)
            : this(prefixes, method) { }

        public void Run()
        {
            try
            {
                ThreadPool.QueueUserWorkItem((o) =>
                {
                    try
                    {
                        while (listener.IsListening)
                        {
                            ThreadPool.QueueUserWorkItem((c) =>
                            {
                                var ctx = c as HttpListenerContext;
                                try
                                {
                                    string rstr = responderMethod(ctx.Request);
                                    var buf = Encoding.UTF8.GetBytes(rstr);
                                    ctx.Response.ContentLength64 = buf.Length;
                                    ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                                }
                                catch { }
                                finally
                                {
                                    ctx.Response.OutputStream.Close();
                                }
                            }, listener.GetContext());
                        }
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Server has an error.", ex);
            }
        }

        public void Stop()
        {
            try
            {
                listener.Stop();
                listener.Close();
            }
            catch (Exception ex)
            {
                throw new Exception("The server can´t stopped.", ex);
            }
        }
    }
}