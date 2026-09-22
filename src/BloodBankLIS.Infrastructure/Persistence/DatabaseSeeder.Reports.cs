using BloodBankLIS.Application.Reporting;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Infrastructure.Reporting;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

public static partial class DatabaseSeeder
{
    /// <summary>
    /// Inserts the eight system reports when their codes are missing, and grants
    /// <c>report.run</c> to operational roles on databases created before reporting.
    /// </summary>
    private static async Task EnsureReportDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var existing = await context.ReportDefinitions.Select(row => row.Code).ToListAsync(ct);
        var missing = ReportSeedCatalog.Create()
            .Where(row => !existing.Contains(row.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count > 0)
        {
            context.ReportDefinitions.AddRange(missing);
            await context.SaveChangesAsync(ct);
        }

        await GrantReportRunAsync(context, ct);
    }

    private static async Task GrantReportRunAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var run = await context.Permissions.FirstOrDefaultAsync(p => p.Code == PermissionCodes.ReportRun, ct);
        if (run is null)
            return;

        var roleNames = new[]
        {
            "Administrator", "Dev Admin", "Supervisor", "Technologist",
            "Inventory Manager", "Auditor", "ReadOnly"
        };
        var roles = await context.Roles.Where(role => roleNames.Contains(role.Name)).ToListAsync(ct);
        var changed = false;
        foreach (var role in roles)
        {
            if (await context.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == run.Id, ct))
                continue;

            context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = run.Id });
            changed = true;
        }

        if (changed)
            await context.SaveChangesAsync(ct);
    }

    private static Task EnsureReportViewsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        _ = ct;
        ReportViewSql.Apply(sql => context.Database.ExecuteSqlRaw(sql), context.Database.ProviderName ?? string.Empty);
        return Task.CompletedTask;
    }
}
