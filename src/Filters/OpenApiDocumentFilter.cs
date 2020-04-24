namespace Ser.Engine.Rest.Filters
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.OpenApi.Models;
    using Microsoft.OpenApi.Writers;
    using NLog;
    using Swashbuckle.AspNetCore.SwaggerGen;
    #endregion

    /// <summary>
    /// Inistalizes the openapi document
    /// </summary>
    public class OpenApiDocumentFilter : IDocumentFilter
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Properties and Variables
        /// <summary>
        /// Urls that are added to the server property.
        /// </summary>
        public List<OpenApiServer> Servers { get; }

        /// <summary>
        /// Write the OpenAPI docs in Json and Yaml
        /// </summary>
        public bool WriteOpenApiDocs { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        public OpenApiDocumentFilter(List<OpenApiServer> servers, bool writeOpenApiDocs)
        {
            Servers = servers ?? new List<OpenApiServer>();
            WriteOpenApiDocs = writeOpenApiDocs;
        }
        #endregion

        /// <summary>
        /// Modify Openapi document
        /// </summary>
        public void Apply(OpenApiDocument openApiDoc, DocumentFilterContext context)
        {
            try
            {
                foreach (var server in Servers)
                {
                    openApiDoc.Servers.Add(server);
                    var serverUri = new Uri(server.Url);
                    var basePath = serverUri?.AbsolutePath ?? null;
                    if (basePath == null)
                        continue;
                    var pathsToModify = openApiDoc.Paths.Where(p => p.Key.StartsWith(basePath)).ToList();
                    foreach (var path in pathsToModify)
                    {
                        if (path.Key.StartsWith(basePath))
                        {
                            string newKey = Regex.Replace(path.Key, $"^{basePath}", String.Empty);
                            openApiDoc.Paths.Remove(path.Key);
                            openApiDoc.Paths.Add(newKey, path.Value);
                        }
                    }
                }

                //Writing OpenAPI document - Yaml and Json
                if (WriteOpenApiDocs)
                {
                    using (var streamWriter = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "OpenAPI.json")))
                    {
                        var jsonWriter = new OpenApiJsonWriter(streamWriter);
                        openApiDoc.SerializeAsV3(jsonWriter);
                    }

                    using (var streamWriter = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "OpenAPI.yaml")))
                    {
                        var yamlWriter = new OpenApiYamlWriter(streamWriter);
                        openApiDoc.SerializeAsV3(yamlWriter);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The OpenAPI documention initialitaion failed.");
            }
        }
    }
}
