using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Web.Security;

/// <summary>Applies <see cref="HttpSecurityHeaderPolicy.WebHeaders"/> to every response.</summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            foreach (var (name, value) in HttpSecurityHeaderPolicy.WebHeaders)
            {
                if (!response.Headers.ContainsKey(name))
                    response.Headers[name] = value;
            }

            return Task.CompletedTask;
        }, context.Response);

        return _next(context);
    }
}
