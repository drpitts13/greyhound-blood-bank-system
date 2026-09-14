using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Auth;

/// <summary>Applies <see cref="HttpSecurityHeaderPolicy.ApiHeaders"/> to every response.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            foreach (var (name, value) in HttpSecurityHeaderPolicy.ApiHeaders)
            {
                if (!response.Headers.ContainsKey(name))
                    response.Headers[name] = value;
            }

            return Task.CompletedTask;
        }, context.Response);

        return _next(context);
    }
}
