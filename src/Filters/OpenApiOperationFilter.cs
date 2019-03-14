#region License
/*
Copyright (c) 2019 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Engine.Rest.Filters
{
    #region Usings
    using System;
    using System.Linq;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.OpenApi.Models;
    using NLog;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http.Internal;
    #endregion

    /// <summary>
    /// Path Parameter Validation Rules Filter
    /// </summary>
    public class OpenApiOperationFilter : IOperationFilter
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="operation">Operation</param>
        /// <param name="context">OperationFilterContext</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            try
            {
                var pars = context.ApiDescription.ParameterDescriptions;
                foreach (var par in pars)
                {
                    var swaggerParam = operation.Parameters.SingleOrDefault(p => p.Name == par.Name);
                    var attributes = ((ControllerParameterDescriptor)par.ParameterDescriptor).ParameterInfo.CustomAttributes;
                    if (attributes != null && attributes.Count() > 0 && swaggerParam != null)
                    {
                        // Set Required Parameter
                        var requiredAttr = attributes.FirstOrDefault(p => p.AttributeType == typeof(RequiredAttribute));
                        if (requiredAttr != null)
                            swaggerParam.Required = true;

                        // Check String Length
                        int? minLength = null;
                        int? maxLength = null;
                        var stringLengthAttr = attributes.FirstOrDefault(p => p.AttributeType == typeof(StringLengthAttribute));
                        if (stringLengthAttr != null)
                        {
                            if (stringLengthAttr.NamedArguments.Count == 1)
                                minLength = (int)stringLengthAttr.NamedArguments.Single(p => p.MemberName == "MinimumLength").TypedValue.Value;
                            maxLength = (int)stringLengthAttr.ConstructorArguments[0].Value;
                        }
                        var minLengthAttr = attributes.FirstOrDefault(p => p.AttributeType == typeof(MinLengthAttribute));
                        if (minLengthAttr != null)
                            minLength = (int)minLengthAttr.ConstructorArguments[0].Value;
                        var maxLengthAttr = attributes.FirstOrDefault(p => p.AttributeType == typeof(MaxLengthAttribute));
                        if (maxLengthAttr != null)
                            maxLength = (int)maxLengthAttr.ConstructorArguments[0].Value;
                        if (swaggerParam is OpenApiParameter)
                        {
                            swaggerParam.Schema.MinLength = minLength;
                            swaggerParam.Schema.MaxLength = maxLength;
                        }

                        //Check the Range
                        var rangeAttr = attributes.FirstOrDefault(p => p.AttributeType == typeof(RangeAttribute));
                        if (rangeAttr != null)
                        {
                            int rangeMin = (int)rangeAttr.ConstructorArguments[0].Value;
                            int rangeMax = (int)rangeAttr.ConstructorArguments[1].Value;
                            if (swaggerParam is OpenApiParameter)
                            {
                                swaggerParam.Schema.Minimum = rangeMin;
                                swaggerParam.Schema.Maximum = rangeMax;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The parameter validation was failed.");
            }
        }
    }
}