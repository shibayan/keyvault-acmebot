using System;
using System.Threading.Tasks;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Internal
{
    public class ApplicationInsightsLoggingMiddleware : IFunctionsWorkerMiddleware
    {
        private readonly ILogger _logger;

        public ApplicationInsightsLoggingMiddleware(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ApplicationInsightsLoggingMiddleware>();
        }

        public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
        {
            try
            {
                _logger.LogInformation("Function {FunctionName} started execution", context.FunctionDefinition.Name);
                
                await next(context);
                
                _logger.LogInformation("Function {FunctionName} completed execution", context.FunctionDefinition.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Function {FunctionName} failed: {ErrorMessage}", context.FunctionDefinition.Name, ex.Message);
                throw;
            }
        }
    }
}