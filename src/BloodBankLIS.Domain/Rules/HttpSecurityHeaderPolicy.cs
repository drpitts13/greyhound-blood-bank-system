namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Browser and API security headers (gap 18). Interactive identity is a Bearer
/// session in circuit memory, not a cookie, so classic cookie CSRF is not the
/// API threat. These headers still block clickjacking, MIME sniffing, and
/// wildcard CORS in Production.
/// </summary>
public static class HttpSecurityHeaderPolicy
{
    public const string CorsAnyCode = "SEC-CORS-ANY";

    public static IReadOnlyList<(string Name, string Value)> ApiHeaders { get; } =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; base-uri 'none'"),
        ("Cache-Control", "no-store"),
        ("Permissions-Policy", "camera=(), microphone=(), geolocation=()")
    ];

    /// <summary>
    /// Blazor Server needs inline script/style and eval for the circuit. That
    /// residual is recorded; do not loosen <c>frame-ancestors</c>.
    /// </summary>
    public static IReadOnlyList<(string Name, string Value)> WebHeaders { get; } =
    [
        ("X-Content-Type-Options", "nosniff"),
        ("X-Frame-Options", "DENY"),
        ("Referrer-Policy", "no-referrer"),
        ("Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; " +
            "connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'"),
        ("Permissions-Policy", "camera=(), microphone=(), geolocation=()")
    ];

    public static RuleResult EvaluateCorsAllowAny(bool allowAnyOrigin, bool isDevelopment) =>
        allowAnyOrigin && !isDevelopment
            ? RuleResult.HardStop(
                CorsAnyCode,
                "CORS AllowAnyOrigin is not permitted outside Development.")
            : RuleResult.Pass(CorsAnyCode);
}
