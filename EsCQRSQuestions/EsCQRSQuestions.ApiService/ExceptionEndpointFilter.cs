public class ExceptionEndpointFilter(ILogger<ExceptionEndpointFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception ex)
        {
            var statusCode = ex switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            if (statusCode >= 500)
            {
                logger.LogError(ex, "Unhandled exception in endpoint filter");
            }
            else
            {
                logger.LogWarning(ex, "Handled endpoint exception with status code {StatusCode}", statusCode);
            }

            return Results.Problem(
                statusCode: statusCode,
                title: ex.GetType().FullName,
                detail: ex.Message);
        }
    }
}
