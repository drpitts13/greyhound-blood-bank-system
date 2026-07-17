using BloodBankLIS.Application.Common;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Api.Endpoints;

/// <summary>
/// Shared mapping from <see cref="OperationResult{T}"/> to HTTP results so endpoints
/// surface warnings, not-found, and validation failures consistently.
/// </summary>
internal static class EndpointResults
{
    public static IResult From<T>(OperationResult<T> result, Func<T, object> map)
    {
        if (result.Succeeded)
        {
            var payload = map(result.Value!);
            return result.HasWarnings
                ? Results.Ok(new { data = payload, warnings = result.Warnings.Select(w => new { w.Code, w.Message }) })
                : Results.Ok(payload);
        }

        return ToFailure(result.Error);
    }

    public static IResult Created<T>(OperationResult<T> result, Func<T, (string location, object body)> map)
    {
        if (!result.Succeeded)
        {
            return ToFailure(result.Error);
        }

        var (location, body) = map(result.Value!);
        return result.HasWarnings
            ? Results.Created(location, new { data = body, warnings = result.Warnings.Select(w => new { w.Code, w.Message }) })
            : Results.Created(location, body);
    }

    private static IResult ToFailure(string? error) =>
        error is not null && error.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? Results.NotFound(new { error })
            : Results.BadRequest(new { error });

    public static IResult FromEvaluation<T>(EvaluationResult<T> result, Func<T, object> map)
    {
        if (result.Succeeded)
        {
            return WithEvaluation(map(result.Value!), result.Evaluation);
        }

        return result.Evaluation is not null ? Blocked(result.Evaluation) : ToFailure(result.Error);
    }

    public static IResult CreatedEvaluation<T>(EvaluationResult<T> result, Func<T, (string location, object body)> map)
    {
        if (!result.Succeeded)
        {
            return result.Evaluation is not null ? Blocked(result.Evaluation) : ToFailure(result.Error);
        }

        var (location, body) = map(result.Value!);
        return Results.Created(location, WithEvaluationPayload(body, result.Evaluation));
    }

    private static IResult WithEvaluation(object payload, RuleEvaluation? evaluation) =>
        Results.Ok(WithEvaluationPayload(payload, evaluation));

    private static object WithEvaluationPayload(object payload, RuleEvaluation? evaluation)
    {
        if (evaluation is null || evaluation.Warnings.Count == 0)
        {
            return payload;
        }

        return new { data = payload, warnings = evaluation.Warnings.Select(r => new { r.Code, r.Message }) };
    }

    private static IResult Blocked(RuleEvaluation evaluation) =>
        Results.UnprocessableEntity(new
        {
            blocked = true,
            overridable = evaluation.RequiresOverride,
            hardStops = evaluation.HardStops.Select(r => new { r.Code, r.Message }),
            warnings = evaluation.Warnings.Select(r => new { r.Code, r.Message })
        });
}
