namespace SapForm
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ApiErrorException ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                if (ex.Message == "Unauthorized, session expired.")
                {
                    context.Response.StatusCode = 401;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");

                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    Message = "Something went wrong",
                    TraceId = context.TraceIdentifier
                });
            }
        }
    }
}