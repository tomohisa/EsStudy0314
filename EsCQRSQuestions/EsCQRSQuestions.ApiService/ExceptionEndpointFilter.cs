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
            logger.LogError(ex, "Unhandled exception in endpoint filter");
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: ex.GetType().FullName,
                detail: ex.Message);
        }
    }
}
