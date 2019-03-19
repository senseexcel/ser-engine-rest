#region License
/*
Copyright (c) 2019 Konrad Mattheis und Martin Berthold
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
#endregion

namespace Ser.Engine.Rest.Attributes
{
    #region Usings
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Mvc.ModelBinding;
    using NLog;
    #endregion

    /// <summary>
    /// Model state validation attribute
    /// </summary>
    public class ValidateModelStateAttribute : ActionFilterAttribute
    {
        #region Logger
        private static Logger logger = LogManager.GetCurrentClassLogger();
        #endregion

        #region Private Methods
        private void EvaluateValidationAttributes(ParameterInfo parameter, object argument, ModelStateDictionary modelState)
        {
            var validationAttributes = parameter.CustomAttributes;
            foreach (var attributeData in validationAttributes)
            {
                var attributeInstance = CustomAttributeExtensions.GetCustomAttribute(parameter, attributeData.AttributeType);
                if (attributeInstance is ValidationAttribute validationAttribute)
                {
                    var isValid = validationAttribute.IsValid(argument);
                    if (isValid)
                        logger.Debug($"The Parameter {parameter?.Name} is vaild.");
                    else
                    {
                        logger.Debug($"The Parameter {parameter?.Name} is not vaild.");
                        modelState.AddModelError(parameter.Name, validationAttribute.FormatErrorMessage(parameter.Name));
                    }
                }
                else if (attributeInstance is FromBodyAttribute bodyAttribute)
                {
                    if (argument != null)
                        modelState.Root.ValidationState = ModelValidationState.Valid;
                }
            }
        }

        private object ParseDataType(string value)
        {
            if (Boolean.TryParse(value, out var boolResult))
                return boolResult;
            else if (Int32.TryParse(value, out var int32Result))
                return int32Result;
            else if (Int64.TryParse(value, out var int64Result))
                return int64Result;
            else if (Double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleResult))
                return doubleResult;
            else
                return value;
        }
        #endregion

        /// <summary>
        /// Called before the action method is invoked
        /// </summary>
        /// <param name="context">Validation Context</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            try
            {
                if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
                {
                    foreach (var parameter in descriptor.MethodInfo.GetParameters())
                    {
                        object argument = null;
                        if (context.ActionArguments.ContainsKey(parameter.Name))
                            argument = context.ActionArguments[parameter.Name];
                        else
                        {
                            var headers = context?.HttpContext?.Request?.Headers ?? null;
                            if (headers != null && headers.ContainsKey(parameter.Name))
                            {
                                var value = headers[parameter.Name];
                                argument = ParseDataType(value);
                                context.ActionArguments.Add(parameter.Name, argument);
                            }
                            else
                            {
                                var stream = context?.HttpContext?.Request?.Body ?? null;
                                if (stream != null && stream.CanSeek && parameter.ParameterType == typeof(string))
                                {
                                    stream.Position = 0;
                                    using (var reader = new StreamReader(stream))
                                    {
                                        var bodyContent = reader.ReadToEnd();
                                        argument = bodyContent;
                                        context.ActionArguments.Add(parameter.Name, bodyContent);
                                    }
                                }
                                else
                                    logger.Warn($"Unhandled validation value - parameter {parameter.Name}");
                            }
                        }

                        EvaluateValidationAttributes(parameter, argument, context.ModelState);
                    }
                }

                if (!context.ModelState.IsValid)
                    context.Result = new BadRequestObjectResult(context.ModelState);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "The validation check failed.");
            }
        }
    }
}