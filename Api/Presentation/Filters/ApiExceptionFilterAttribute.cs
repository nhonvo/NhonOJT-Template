
using Api.Infrastructure.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;
using System.Diagnostics;
using System.Transactions;

namespace Api.Presentation.Filters
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        private readonly IDictionary<Type, Action<ExceptionContext>> _exceptionHandlers;
        public ApiExceptionFilterAttribute()
        {
            _exceptionHandlers = new Dictionary<Type, Action<ExceptionContext>>
        {
            { typeof(InvalidOperationException), HandleInvalidOperationException },
            { typeof(ValidationException), HandleValidationException },
            { typeof(ArgumentNullException), HandleNotFoundException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
            { typeof(TransactionException), HandleTransactionException },
        };
        }

        public override void OnException(ExceptionContext context)
        {
            HandleException(context);

            base.OnException(context);
        }

        private void HandleException(ExceptionContext context)
        {
            Type type = context.Exception.GetType();
            if (_exceptionHandlers.TryGetValue(type, out Action<ExceptionContext>? value))
            {
                value.Invoke(context);
                return;
            }

            if (!context.ModelState.IsValid)
            {
                HandleInvalidModelStateException(context);
                return;
            }
        }
        private void HandleInvalidOperationException(ExceptionContext context)
        {
            var exception = (InvalidOperationException)context.Exception;

            // Log the exception details
            Trace.TraceError($"An invalid operation exception occurred: {exception.Message}");
            Log.Error($"An invalid operation exception occurred: {exception.Message}");
            NewRelicExtension.ErrorCustomMonitor("System", $"An invalid operation exception occurred: {exception.Message}");

            var details = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = exception.Message,
                Instance = context.HttpContext.Request.Path
            };

            context.Result = new ObjectResult(details)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

            context.ExceptionHandled = true;
        }

        private void HandleValidationException(ExceptionContext context)
        {
            var exception = (ValidationException)context.Exception;

            Log.Error($"A Validation exception occurred: {exception.Message}");

            var modelState = exception.Errors
                .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                .ToDictionary(failureGroup => failureGroup.Key, failureGroup => failureGroup.ToArray());

            var details = new ValidationProblemDetails(modelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            context.Result = new BadRequestObjectResult(details);

            context.ExceptionHandled = true;
        }
        private static void HandleInvalidModelStateException(ExceptionContext context)
        {
            Log.Error($"An Invalid Model exception occurred: {context.Exception.Message}");

            var details = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            context.Result = new BadRequestObjectResult(details);

            context.ExceptionHandled = true;
        }
        private void HandleNotFoundException(ExceptionContext context)
        {
            var exception = context.Exception;
            Log.Error($"An Not Found exception occurred: {exception.Message}");
            NewRelicExtension.ErrorCustomMonitor("System", $"An Not Found exception occurred: {exception.Message}");
            var details = new ProblemDetails()
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "The specified resource was not found.",
                Detail = exception.Message
            };

            context.Result = new NotFoundObjectResult(details);

            context.ExceptionHandled = true;
        }
        private void HandleUnauthorizedAccessException(ExceptionContext context)
        {
            var exception = (UnauthorizedAccessException)context.Exception;
            Log.Error($"An Unauthorized Access exception occurred: {context.Exception.Message}");
            NewRelicExtension.ErrorCustomMonitor("System", $"An Unauthorized Access exception occurred: {exception.Message}");

            var details = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            };

            context.Result = new ObjectResult(details)
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };

            context.ExceptionHandled = true;
        }
        private void HandleTransactionException(ExceptionContext context)
        {
            var exception = (TransactionException)context.Exception;
            Log.Error($"Something went wrong in this transaction: {context.Exception.Message}");
            NewRelicExtension.ErrorCustomMonitor("System", $"Something went wrong in this transaction: {exception.Message}");

            var details = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = context.Exception.Message,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            };

            context.Result = new ObjectResult(details)
            {
                StatusCode = StatusCodes.Status400BadRequest
            };

            context.ExceptionHandled = true;
        }
    }
}