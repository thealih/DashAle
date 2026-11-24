using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PriceTracker.Api.Models.DTOs;
using PriceTracker.Application.Models.Shared;
using PriceTracker.Application.Models.DTOs;

using PriceTracker.Application.Features.Panel;
using PriceTracker.Api.Panel;

namespace PriceTracker.Application.Middlewares
{
    public class ExceptionHandlingMiddleware : IExceptionHandler
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is PanelException panelException)
            {
                await PanelSecurity.Error(httpContext, panelException.StatusCode, panelException.Message);
                return true;
            }

            var detail = GetExceptionDetail(exception);

            _logger.LogError($"{nameof(ExceptionHandlingMiddleware)}" +
                             $"\n - path : {httpContext.Request.Path}" +
                             $"\n - class : {detail.Class}" +
                             $"\n - method : {detail.Method}" +
                             $"\n - line number : {detail.LineNumber}" +
                             $"\n - exception : {detail.Message}" +
                             $"\n - inner exception : {detail.InnerExceptionMessage}");

            var message = string.Empty;

            if (exception is ValidationException)
            {
                message = exception.Message;
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
            else
            {
                message = "Unexpected error occurred.";
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            var response = Response.IsFailure(message);
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true;
        }

        private ExceptionDetailDTO GetExceptionDetail(Exception ex)
        {
            var stackTrace = new StackTrace(ex, true);
            var frame = stackTrace.GetFrame(0);
            var type = frame?.GetMethod()?.DeclaringType;

            var methodName = "-";
            if (!string.IsNullOrWhiteSpace(type?.Name))
            {
                var methodNameRegex = @"<([^>]+)>";
                var match = Regex.Match(type.Name, methodNameRegex);
                if (match.Success) methodName = match.Groups[1].Value;
            }

            ExceptionDetailDTO detail = new()
            {
                Message = ex.Message,
                InnerExceptionMessage =
                    string.IsNullOrWhiteSpace(ex.InnerException?.Message)
                    ? "null" : ex.InnerException?.Message,
                Class = type?.DeclaringType?.Name,
                Method = methodName,
                LineNumber = frame?.GetFileLineNumber()
            };

            return detail;
        }
    }
}
