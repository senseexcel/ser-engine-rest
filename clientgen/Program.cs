namespace Ser.Engine.Rest.Client
{
    #region Usings
    using Microsoft.OpenApi;
    using Microsoft.OpenApi.Extensions;
    using Microsoft.OpenApi.Readers;
    using NLog;
    using NLog.Config;
    using NSwag;
    using NSwag.CodeGeneration.CSharp;
    using NSwag.CodeGeneration.TypeScript;
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    #endregion

    class Program
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        static void Main(string[] args)
        {
            try
            {
                SetLoggerSettings("App.config");
                Console.WriteLine("Generate Client file for SER Connector...");

                //Path to the OPEN API File
                var basePath = Path.GetFullPath($"{AppContext.BaseDirectory}..\\..\\..\\..");
                var openApiFile = Path.Combine(basePath, "src\\bin\\Debug\\netcoreapp3.1\\OpenAPI.json");

                //Create new .cs file from Api
                //BUG: with Stream in 3.0 use 2.0 for generator
                CreateCsFile(openApiFile);
                Console.WriteLine("Finish");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
                Console.ReadLine();
            }
        }

        private static void CreateCsFile(string filename)
        {
            var stream = File.OpenRead(filename);
            var openApiDocument = new OpenApiStreamReader().Read(stream, out var diagnostic);
            var outputString = openApiDocument.Serialize(OpenApiSpecVersion.OpenApi2_0, OpenApiFormat.Json);
            filename = Path.Combine(AppContext.BaseDirectory, "openapi20.json");
            File.WriteAllText(filename, outputString);

            var openapiDoc = OpenApiDocument.FromFileAsync(filename).Result;
            var settings = new CSharpClientGeneratorSettings()
            {
                ClassName = "SerApiClient",
                CSharpGeneratorSettings =
                {
                    Namespace = "Ser.Engine.Rest.Client",
                }
            };
            var generator = new CSharpClientGenerator(openapiDoc, settings);
            var code = generator.GenerateFile(NSwag.CodeGeneration.ClientGeneratorOutputType.Full);
            var codePath = Path.Combine(AppContext.BaseDirectory.Replace("\\bin\\Debug\\netcoreapp3.1\\", "\\"), "SerRestClient.cs");
            File.WriteAllText(codePath, code, Encoding.UTF8);
        }

        #region nlog helper for netcore
        private static void SetLoggerSettings(string configName)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName);
            if (!File.Exists(path))
            {
                var root = new FileInfo(path).Directory?.Parent?.Parent?.Parent;
                var files = root.GetFiles("App.config", SearchOption.AllDirectories).ToList();
                path = files.FirstOrDefault()?.FullName;
            }
            LogManager.Configuration = new XmlLoggingConfiguration(path, false);
        }
        #endregion
    }
}
