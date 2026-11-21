using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Rules;

namespace BloodBankLIS.Application.Tests;

// TEST-BB-397: every facility policy the service reads is listed on Facility Policies, and the default the service uses
// when the setting is unset is the default the catalog shows administrators. A new key or a changed default cannot
// drift between the two.
public class FacilityPolicyDefaultsCoverageTests
{
    /// <summary>Keys kept only so old databases still load; nothing reads them.</summary>
    private static readonly HashSet<string> Retired = new(StringComparer.Ordinal) { FacilityPolicyKeys.BlockRetypeSelfVerify };

    /// <summary>A settings store with no rows that records which key each read asked for.</summary>
    private sealed class EmptySettings : IRepository<SystemSetting>
    {
        public List<Expression<Func<SystemSetting, bool>>> Reads { get; } = new();

        public Task<SystemSetting?> FirstOrDefaultAsync(Expression<Func<SystemSetting, bool>> predicate, CancellationToken cancellationToken = default)
        {
            Reads.Add(predicate);
            return Task.FromResult<SystemSetting?>(null);
        }

        public Task<SystemSetting?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<SystemSetting?>(null);
        public Task<IReadOnlyList<SystemSetting>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemSetting>>([]);
        public Task<IReadOnlyList<SystemSetting>> ListAsync(Expression<Func<SystemSetting, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SystemSetting>>([]);
        public Task<bool> AnyAsync(Expression<Func<SystemSetting, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(SystemSetting entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(SystemSetting entity) { }
        public IQueryable<SystemSetting> Query() => Enumerable.Empty<SystemSetting>().AsQueryable();
    }

    private static IEnumerable<MethodInfo> Getters() =>
        typeof(FacilityPolicyService).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("Get", StringComparison.Ordinal)
                && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(CancellationToken)
                && (m.ReturnType == typeof(Task<bool>) || m.ReturnType == typeof(Task<int>)));

    /// <summary>Calls one getter on an empty store: the key it read (matched against the known keys) and the default it returned.</summary>
    private static async Task<(string? Key, object Value)> ProbeAsync(MethodInfo getter)
    {
        var settings = new EmptySettings();
        var task = (Task)getter.Invoke(new FacilityPolicyService(settings), [CancellationToken.None])!;
        await task;
        var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var read = Assert.Single(settings.Reads).Compile();
        var key = AllKeys().SingleOrDefault(k => read(new SystemSetting { Key = k }));
        return (key, value);
    }

    private static IReadOnlyList<string> AllKeys() =>
        typeof(FacilityPolicyKeys).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

    [Fact]
    public void EveryKey_IsInTheCatalog_UnlessRetired()
    {
        var catalog = FacilityPolicyCatalog.All.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in AllKeys().Where(k => !Retired.Contains(k)))
        {
            Assert.True(catalog.Contains(key), $"{key} is a facility policy key but is not listed on Facility Policies.");
        }
    }

    [Fact]
    public async Task EveryGetter_ReadsACatalogKey_AndUsesTheCatalogDefault()
    {
        var getters = Getters().ToList();
        Assert.NotEmpty(getters);
        var problems = new List<string>();
        foreach (var getter in getters)
        {
            var (key, value) = await ProbeAsync(getter);
            var definition = FacilityPolicyCatalog.All.SingleOrDefault(d => d.Key == key);
            var shown = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            if (key is null)
            {
                problems.Add($"{getter.Name} reads a key that is not a FacilityPolicyKeys constant.");
            }
            else if (definition is null)
            {
                problems.Add($"{getter.Name} reads {key}, which is not listed on Facility Policies.");
            }
            else if (!string.Equals(shown, definition.DefaultValue, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{getter.Name} defaults {key} to {shown}, but Facility Policies shows {definition.DefaultValue}.");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
