using System.Linq.Expressions;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Entities.Identity;
using BloodBankLIS.Domain.Enums;
using BloodBankLIS.Domain.Rules;
using BloodBankLIS.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BloodBankLIS.Infrastructure.Persistence;

/// <summary>
/// Seeds reference and demo data for development, demos, and workflow validation
/// (see docs/validation-plan.md section 3). Idempotent: skips full tables, and ensures
/// required reference codes exist after a partial SQLite-to-SQL Server migration.
/// </summary>
public static partial class DatabaseSeeder
{
    public static async Task SeedAsync(BloodBankDbContext context, bool seedDevAdmin = false, CancellationToken cancellationToken = default)
    {
        await SeedIdentityAsync(context, cancellationToken);
        await SeedSystemSettingsAsync(context, cancellationToken);
        await EnsureRoleSecurityLevelsAsync(context, cancellationToken);
        await SeedExceptionDefinitionsAsync(context, cancellationToken);
        await SeedProductTypesAsync(context, cancellationToken);
        await EnsureCellularProductCrossmatchFlagsAsync(context, cancellationToken);
        await EnsureModificationProductTypesAsync(context, cancellationToken);
        await EnsureProductTypeIsbtCodesAsync(context, cancellationToken);
        await SeedProductAttributesAsync(context, cancellationToken);
        await SeedBloodAttributeDefinitionsAsync(context, cancellationToken);
        await SeedSpecimenTypeDefinitionsAsync(context, cancellationToken);
        await SeedSubtestDefinitionsAsync(context, cancellationToken);
        await SeedPhaseDefinitionsAsync(context, cancellationToken);
        await SeedTestDefinitionsAsync(context, cancellationToken);
        await EnsureAgtypeTestAsync(context, cancellationToken);
        await EnsureWeakDTestAsync(context, cancellationToken);
        await EnsureNeonatalTypeAndScreenTestAsync(context, cancellationToken);
        await EnsureCrossmatchTestsAsync(context, cancellationToken);
        await EnsureAbscPanelAsync(context, cancellationToken);
        await MigrateExistingTestPanelConfigAsync(context, cancellationToken);
        await SeedTestGroupersAsync(context, cancellationToken);
        await SeedReflexRulesAsync(context, cancellationToken);
        await SeedRuleDefinitionsAsync(context, cancellationToken);
        await SeedLocationsAsync(context, cancellationToken);
        await SeedOrderingLocationsAsync(context, cancellationToken);
        await SeedOrderingProvidersAsync(context, cancellationToken);
        await SeedChargeMasterAsync(context, cancellationToken);
        await SeedExpirationModificationCodesAsync(context, cancellationToken);
        await SeedModificationRulesAsync(context, cancellationToken);
        await SeedDemoClinicalDataAsync(context, cancellationToken);
        await EnsureIsbtPermissionsAsync(context, cancellationToken);
        await SeedIsbt128LookupsAsync(context, cancellationToken);
        await SeedExtendedDemoScenariosAsync(context, cancellationToken);

        if (seedDevAdmin)
        {
            await SeedDevAdminAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Ensures newly introduced permission codes exist on upgraded databases.
    /// </summary>
    private static async Task EnsureIsbtPermissionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var existing = await context.Permissions.Select(p => p.Code).ToListAsync(ct);
        var missing = PermissionCodes.All.Except(existing, StringComparer.Ordinal).ToList();
        if (missing.Count == 0)
            return;

        context.Permissions.AddRange(missing.Select(code => new Permission { Code = code }));
        await context.SaveChangesAsync(ct);

        // Grant new ISBT/inventory permissions to Administrator (and Dev Admin if present).
        var adminRoles = await context.Roles
            .Where(r => r.Name == "Administrator" || r.Name == "Dev Admin" || r.Name == "Supervisor")
            .ToListAsync(ct);
        var perms = await context.Permissions.Where(p => missing.Contains(p.Code)).ToListAsync(ct);
        foreach (var role in adminRoles)
        {
            foreach (var perm in perms)
            {
                if (!await context.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id, ct))
                {
                    context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                }
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedSystemSettingsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.SystemSettings.AnyAsync(ct))
        {
            return;
        }

        context.SystemSettings.AddRange(
            new SystemSetting
            {
                Key = FacilityPolicyKeys.SpecimenAlloimmunizationHours,
                Value = "72",
                Category = "Specimen",
                Description = "AABB-style 3-day window when transfused or pregnant in the lookback period."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.SpecimenStandardHours,
                Value = "168",
                Category = "Specimen",
                Description = "Standard specimen validity (hours) when alloimmunization risk is not present."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.SpecimenLookbackDays,
                Value = "90",
                Category = "Specimen",
                Description = "Lookback days for recent transfusion or pregnancy."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.RequireSecondVerifier,
                Value = "true",
                Category = "Transfusion",
                Description = "Require a distinct second verifier or validated electronic identification."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.BlockSelfVerify,
                Value = "false",
                Category = "Result",
                Description = "When true, the entering user cannot verify the same result."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.RetentionYears,
                Value = "10",
                Category = "Records",
                Description = "Minimum product record retention (21 CFR 606.160(d)). No automated purge."
            },
            new SystemSetting
            {
                Key = FacilityPolicyKeys.SignatureValidityMinutes,
                Value = "15",
                Category = "Signatures",
                Description = "Electronic signature reuse window before consumption."
            });
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// PLACEHOLDER ISBT lookup rows for demonstration/testing only.
    /// Do not treat as official ICCBBA code tables. ICCBBA_VALIDATION_REQUIRED.
    /// </summary>
    private static async Task SeedIsbt128LookupsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.IsbtDataStructures.AnyAsync(ct))
        {
            context.IsbtDataStructures.AddRange(
                new IsbtDataStructure { DataIdentifier = "=", Kind = IsbtDataStructureKind.DonationIdentificationNumber, Description = "DIN (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "=%", Kind = IsbtDataStructureKind.AboRhd, Description = "ABO/RhD (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "=<", Kind = IsbtDataStructureKind.ProductCode, Description = "Product (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "=>", Kind = IsbtDataStructureKind.ExpirationDate, Description = "Expiration date (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "&>", Kind = IsbtDataStructureKind.ExpirationDateTime, Description = "Expiration date/time (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "=*", Kind = IsbtDataStructureKind.CollectionDate, Description = "Collection date (PLACEHOLDER)" },
                new IsbtDataStructure { DataIdentifier = "&*", Kind = IsbtDataStructureKind.CollectionDateTime, Description = "Collection date/time (PLACEHOLDER)" });
            await context.SaveChangesAsync(ct);
        }

        if (!await context.IsbtCollectionTypes.AnyAsync(ct))
        {
            context.IsbtCollectionTypes.Add(new IsbtCollectionType
            {
                Code = "0",
                Description = "PLACEHOLDER — Volunteer/allogeneic (requires ICCBBA confirmation)",
                IsPlaceholder = true
            });
            await context.SaveChangesAsync(ct);
        }

        if (!await context.IsbtAboRhdCodes.AnyAsync(ct))
        {
            // Intentionally fabricated facility demo codes — NOT official ISBT ABO/RhD encodings.
            context.IsbtAboRhdCodes.AddRange(
                new IsbtAboRhdCode
                {
                    Code = "DEMO",
                    Abo = AboGroup.O,
                    RhD = RhType.Positive,
                    CollectionType = "Volunteer/allogeneic (PLACEHOLDER)",
                    SpecialMessage = "PLACEHOLDER demo code — replace with ICCBBA table",
                    IsPlaceholder = true
                },
                new IsbtAboRhdCode
                {
                    Code = "DEMA",
                    Abo = AboGroup.A,
                    RhD = RhType.Negative,
                    CollectionType = "Volunteer/allogeneic (PLACEHOLDER)",
                    IsPlaceholder = true
                });
            await context.SaveChangesAsync(ct);
        }

        await SeedUsSupplierProductCodesAsync(context, ct);

        if (!await context.CompatibilityRuleVersions.AnyAsync(ct))
        {
            var version = new CompatibilityRuleVersion
            {
                Version = "PLACEHOLDER-1",
                PolicyVersion = "PLACEHOLDER-POLICY-1",
                EffectiveDate = new DateOnly(2020, 1, 1),
                IsActive = true,
                Notes = "INSTITUTIONAL_POLICY_REVIEW / MEDICAL_DIRECTOR_APPROVAL required before clinical use."
            };
            context.CompatibilityRuleVersions.Add(version);
            await context.SaveChangesAsync(ct);

            context.CompatibilityRules.Add(new CompatibilityRule
            {
                CompatibilityRuleVersionId = version.Id,
                RuleCode = "RBC-ABO-BASE",
                ComponentClass = ComponentClass.RedBloodCells,
                RuleFamily = "RedBloodCells",
                ExpressionJson = "{}",
                Severity = "HardStop",
                Description = "Delegates to domain AboCompatibilityRule (placeholder version row)."
            });
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Idempotent upsert of commonly published US supplier ISBT PDCs into <see cref="IsbtProductCode"/>.
    /// Inserts missing codes and refreshes description/component class for seed rows.
    /// </summary>
    private static async Task SeedUsSupplierProductCodesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var seedRows = UsSupplierProductCodeSeed.CreateRows();
        var existing = await context.IsbtProductCodes.ToListAsync(ct);
        var byPdc = existing
            .GroupBy(r => r.ProductDescriptionCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var changed = false;
        foreach (var seed in seedRows)
        {
            if (byPdc.TryGetValue(seed.ProductDescriptionCode, out var rows))
            {
                // Unique index is (PDC, StandardVersion); collapse legacy duplicates for the same PDC first.
                for (var i = 1; i < rows.Count; i++)
                {
                    context.IsbtProductCodes.Remove(rows[i]);
                    changed = true;
                }

                var row = rows[0];
                if (row.Description != seed.Description
                    || row.ComponentClass != seed.ComponentClass
                    || row.StandardVersion != seed.StandardVersion
                    || row.AttributesJson != seed.AttributesJson
                    || row.RequiresExtendedDivision != seed.RequiresExtendedDivision
                    || !row.IsPlaceholder)
                {
                    row.Description = seed.Description;
                    row.ComponentClass = seed.ComponentClass;
                    row.StandardVersion = seed.StandardVersion;
                    row.AttributesJson = seed.AttributesJson;
                    row.RequiresExtendedDivision = seed.RequiresExtendedDivision;
                    row.IsPlaceholder = seed.IsPlaceholder;
                    changed = true;
                }
            }
            else
            {
                context.IsbtProductCodes.Add(seed);
                changed = true;
            }
        }

        if (changed)
            await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the no-login dev-mode account (<c>DEV_ADMIN</c>) with a Dev Admin role that
    /// holds every permission. Only invoked when dev mode is enabled in a Development host.
    /// Idempotent.
    /// </summary>
    public static async Task SeedDevAdminAsync(BloodBankDbContext context, CancellationToken ct = default)
    {
        const string devUser = "DEV_ADMIN";

        var permissions = await context.Permissions.ToListAsync(ct);
        if (permissions.Count == 0)
        {
            return;
        }

        var byCode = permissions.ToDictionary(p => p.Code, StringComparer.Ordinal);

        var devRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Dev Admin", ct);
        if (devRole is null)
        {
            devRole = AddRole(context, "Dev Admin", "No-login development administrator (all permissions)", byCode, 3, PermissionCodes.All.ToArray());
            await context.SaveChangesAsync(ct);
        }
        else if (devRole.SecurityLevel < 3)
        {
            devRole.SecurityLevel = 3;
            await context.SaveChangesAsync(ct);
        }

        var user = await context.Users.FirstOrDefaultAsync(u => u.UserName == devUser, ct);
        if (user is null)
        {
            AddUser(context, devUser, "Development Administrator", devRole);
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Seeds the full permission catalog, the standard roles, and demo accounts
    /// (see docs/validation-plan.md section 3). Authorization evaluates permission
    /// codes; roles only aggregate them.
    /// </summary>
    private static async Task SeedIdentityAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Permissions.AnyAsync(ct))
        {
            return;
        }

        var permissions = PermissionCodes.All
            .Select(code => new Permission { Code = code })
            .ToList();
        context.Permissions.AddRange(permissions);
        await context.SaveChangesAsync(ct);

        var byCode = permissions.ToDictionary(p => p.Code, StringComparer.Ordinal);

        // Technologist: routine bench work. No override/discard/correction/cancellation.
        var technologistCodes = new[]
        {
            PermissionCodes.PatientWrite,
            PermissionCodes.SpecimenAccession, PermissionCodes.SpecimenReject,
            PermissionCodes.ResultEnter, PermissionCodes.ResultVerify,
            PermissionCodes.ImmunoRecord,
            PermissionCodes.InventoryReceive, PermissionCodes.InventoryTransfer, PermissionCodes.InventoryRelease,
            PermissionCodes.InventoryModify,
            PermissionCodes.CompatibilityCrossmatch, PermissionCodes.CompatibilityAllocate,
            PermissionCodes.IssueCreate, PermissionCodes.IssueReturn, PermissionCodes.TransfusionDocument,
            PermissionCodes.PrintLabel,
            PermissionCodes.BillingReview,
            PermissionCodes.Hl7Manage,
            PermissionCodes.AuditRead
        };

        // Supervisor: technologist plus the dangerous/override actions.
        var supervisorCodes = technologistCodes.Concat(new[]
        {
            PermissionCodes.ResultCorrect,
            PermissionCodes.ImmunoOverride,
            PermissionCodes.InventoryDiscard,
            PermissionCodes.IssueOverride,
            PermissionCodes.PrintReprint,
            PermissionCodes.BillingCancel, PermissionCodes.BillingExport,
            PermissionCodes.LookbackManage, PermissionCodes.ReactionInvestigate, PermissionCodes.DeviationManage
        }).Distinct().ToArray();

        // Specialized administrative roles. Each gets read access to all config plus
        // edit/activate on its own area (least privilege).
        var interfaceAnalystCodes = new[]
        {
            PermissionCodes.Hl7Manage, PermissionCodes.AuditRead,
            PermissionCodes.AdminConfigView, PermissionCodes.AdminConfigEdit, PermissionCodes.AdminConfigActivate,
            PermissionCodes.AdminHl7Manage, PermissionCodes.AdminAuditReview
        };
        var inventoryManagerCodes = new[]
        {
            PermissionCodes.InventoryReceive, PermissionCodes.InventoryTransfer, PermissionCodes.InventoryRelease, PermissionCodes.InventoryDiscard,
            PermissionCodes.InventoryModify,
            PermissionCodes.AdminConfigView, PermissionCodes.AdminConfigEdit, PermissionCodes.AdminConfigActivate,
            PermissionCodes.AdminProductsManage, PermissionCodes.AdminModificationRulesManage
        };
        var billingAnalystCodes = new[]
        {
            PermissionCodes.BillingReview, PermissionCodes.BillingCancel, PermissionCodes.BillingExport,
            PermissionCodes.AdminConfigView
        };
        var auditorCodes = new[]
        {
            PermissionCodes.AuditRead, PermissionCodes.AdminConfigView, PermissionCodes.AdminAuditReview
        };
        // Non-interactive interface engine / system integrations.
        var serviceAccountCodes = new[] { PermissionCodes.Hl7Manage };

        var administrator = AddRole(context, "Administrator", "Full system access", byCode, 3, PermissionCodes.All.ToArray());
        var supervisor = AddRole(context, "Supervisor", "Bench work plus overrides and dangerous actions", byCode, 2, supervisorCodes);
        var technologist = AddRole(context, "Technologist", "Routine bench work", byCode, 1, technologistCodes);
        var readOnly = AddRole(context, "ReadOnly", "Audit and read-only access", byCode, 0, PermissionCodes.AuditRead);
        var interfaceAnalyst = AddRole(context, "Interface Analyst", "HL7 interface configuration and monitoring", byCode, 0, interfaceAnalystCodes);
        var inventoryManager = AddRole(context, "Inventory Manager", "Inventory operations and product configuration", byCode, 0, inventoryManagerCodes);
        var billingAnalyst = AddRole(context, "Billing Analyst", "Billing review, cancel, and export", byCode, 0, billingAnalystCodes);
        var auditor = AddRole(context, "Auditor", "Read-only audit and configuration history review", byCode, 0, auditorCodes);
        var serviceAccount = AddRole(context, "Service Account", "Non-interactive system/service integrations", byCode, 0, serviceAccountCodes);
        await context.SaveChangesAsync(ct);

        AddUser(context, "admin", "System Administrator", administrator);
        AddUser(context, "supervisor", "Sam Supervisor", supervisor);
        AddUser(context, "tech1", "Terry Technologist", technologist);
        AddUser(context, "viewer", "Val Viewer", readOnly);
        AddUser(context, "analyst", "Ana Analyst", interfaceAnalyst);
        AddUser(context, "invmgr", "Ingrid Manager", inventoryManager);
        AddUser(context, "biller", "Bill Biller", billingAnalyst);
        AddUser(context, "auditor", "Ada Auditor", auditor);
        AddUser(context, "svc-interface", "Interface Engine", serviceAccount, isServiceAccount: true);
        await context.SaveChangesAsync(ct);
    }

    private static Role AddRole(
        BloodBankDbContext context,
        string name,
        string description,
        IReadOnlyDictionary<string, Permission> byCode,
        int securityLevel,
        params string[] codes)
    {
        var role = new Role { Name = name, Description = description, SecurityLevel = securityLevel };
        context.Roles.Add(role);
        foreach (var code in codes.Distinct(StringComparer.Ordinal))
        {
            role.RolePermissions.Add(new RolePermission { Permission = byCode[code] });
        }

        return role;
    }

    /// <summary>
    /// Idempotent: applies canonical security levels to known roles after schema upgrade.
    /// </summary>
    private static async Task EnsureRoleSecurityLevelsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var levelByName = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Administrator"] = 3,
            ["Dev Admin"] = 3,
            ["Supervisor"] = 2,
            ["Technologist"] = 1
        };

        var names = levelByName.Keys.ToList();
        var roles = await context.Roles.Where(r => names.Contains(r.Name)).ToListAsync(ct);
        var changed = false;
        foreach (var role in roles)
        {
            if (levelByName.TryGetValue(role.Name, out var level) && role.SecurityLevel != level)
            {
                role.SecurityLevel = level;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedExceptionDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var changed = false;

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            AboRhDeltaRule.DeltaCode,
            "ABO/Rh historical discrepancy",
            "Verified ABO/Rh disagrees with the patient's current historical type. Requires authorized override and Retain or Replace resolution.",
            minSecurityLevel: 2,
            isOverridable: true,
            ct);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            AntibodyHistoryCrossmatchRule.RuleCode,
            "Simple crossmatch with positive antibody screen or history",
            "Simple crossmatch selected for a patient with a current or historical positive antibody screen, or antibody history. Requires authorized override and a comment; complex crossmatch is preferred.",
            minSecurityLevel: 2,
            isOverridable: true,
            ct,
            updateDescriptionIfExists: true);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            AboCompatibilityRule.AboCode,
            "ABO antigen/antibody incompatibility",
            "Donor and recipient ABO types have an antigen/antibody conflict for the product component class. Not overridable.",
            minSecurityLevel: 0,
            isOverridable: false,
            ct);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            AboCompatibilityRule.RhCode,
            "Rh(D) incompatibility",
            "Rh-positive red cells or whole blood cannot be given to an Rh-negative recipient. Not overridable.",
            minSecurityLevel: 0,
            isOverridable: false,
            ct);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            AboCompatibilityRule.UnknownTypeCode,
            "ABO/Rh unknown",
            "Patient or unit ABO/Rh is unknown; compatibility cannot be established. Not overridable.",
            minSecurityLevel: 0,
            isOverridable: false,
            ct);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            BloodAttributeCompatibilityRule.AntigenNegCode,
            "Antigen-negative requirement not met",
            "Patient has a clinically significant antibody (current or historical); RBC or whole blood unit must be typed antigen-negative. Requires authorized override by supervisor or higher.",
            minSecurityLevel: 2,
            isOverridable: true,
            ct,
            updateDescriptionIfExists: true,
            syncPolicyIfExists: true);

        changed |= await EnsureExceptionDefinitionAsync(
            context,
            CrossmatchValidityRule.Code,
            "Compatible crossmatch required",
            "RBC or whole blood (or other crossmatch-required product) requires a compatible, unexpired crossmatch unless emergency release. Not overridable via exception catalog.",
            minSecurityLevel: 0,
            isOverridable: false,
            ct);

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task<bool> EnsureExceptionDefinitionAsync(
        BloodBankDbContext context,
        string ruleCode,
        string name,
        string description,
        int minSecurityLevel,
        bool isOverridable,
        CancellationToken ct,
        bool updateDescriptionIfExists = false,
        bool syncPolicyIfExists = false)
    {
        var existing = await context.ExceptionDefinitions.FirstOrDefaultAsync(e => e.RuleCode == ruleCode, ct);
        if (existing is null)
        {
            context.ExceptionDefinitions.Add(new ExceptionDefinition
            {
                RuleCode = ruleCode,
                Name = name,
                Description = description,
                MinSecurityLevel = minSecurityLevel,
                IsOverridable = isOverridable,
                IsActive = true
            });
            return true;
        }

        var changed = false;
        if (updateDescriptionIfExists
            && (!string.Equals(existing.Description, description, StringComparison.Ordinal)
                || !string.Equals(existing.Name, name, StringComparison.Ordinal)))
        {
            existing.Name = name;
            existing.Description = description;
            changed = true;
        }

        if (syncPolicyIfExists
            && (existing.IsOverridable != isOverridable || existing.MinSecurityLevel != minSecurityLevel || !existing.IsActive))
        {
            existing.IsOverridable = isOverridable;
            existing.MinSecurityLevel = minSecurityLevel;
            existing.IsActive = true;
            changed = true;
        }

        return changed;
    }

    private static void AddUser(BloodBankDbContext context, string userName, string displayName, Role role, bool isServiceAccount = false)
    {
        var user = new User
        {
            UserName = userName,
            DisplayName = displayName,
            IsServiceAccount = isServiceAccount,
            PasswordHash = SecretHasher.Hash("demo")
        };
        user.UserRoles.Add(new UserRole { Role = role });
        context.Users.Add(user);
    }

    private static async Task SeedChargeMasterAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ChargeCodes.AnyAsync(ct))
        {
            return;
        }

        var aboRh = new ChargeCode { Code = "BB-ABORH", Description = "ABO/Rh typing", DefaultAmount = 35.00m, CptCode = "86900" };
        var screen = new ChargeCode { Code = "BB-SCREEN", Description = "Antibody screen", DefaultAmount = 55.00m, CptCode = "86850" };
        var xm = new ChargeCode { Code = "BB-XM", Description = "Crossmatch", DefaultAmount = 75.00m, CptCode = "86920" };
        var rbcIssue = new ChargeCode { Code = "BB-RBC-ISSUE", Description = "Red blood cell unit issued", DefaultAmount = 250.00m, CptCode = "P9021" };
        var unitIssue = new ChargeCode { Code = "BB-UNIT-ISSUE", Description = "Blood product unit issued", DefaultAmount = 200.00m };
        context.ChargeCodes.AddRange(aboRh, screen, xm, rbcIssue, unitIssue);
        await context.SaveChangesAsync(ct);

        context.ChargeRules.AddRange(
            new ChargeRule { TriggerType = BillingTriggerType.TestVerified, TriggerKey = "ABORH", ChargeCodeId = aboRh.Id },
            new ChargeRule { TriggerType = BillingTriggerType.TestVerified, TriggerKey = "ABSC", ChargeCodeId = screen.Id },
            new ChargeRule { TriggerType = BillingTriggerType.TestVerified, TriggerKey = "XM", ChargeCodeId = xm.Id },
            new ChargeRule { TriggerType = BillingTriggerType.TestVerified, TriggerKey = "CXM", ChargeCodeId = xm.Id },
            // Product-specific rule plus a catch-all for any other issued unit.
            new ChargeRule { TriggerType = BillingTriggerType.UnitIssued, TriggerKey = "RBC-LR", ChargeCodeId = rbcIssue.Id },
            new ChargeRule { TriggerType = BillingTriggerType.UnitIssued, TriggerKey = null, ChargeCodeId = unitIssue.Id });

        await context.SaveChangesAsync(ct);
    }

    private static async Task EnsureCxmChargeRuleAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ChargeRules.AnyAsync(
                r => r.TriggerType == BillingTriggerType.TestVerified && r.TriggerKey == "CXM", ct))
        {
            return;
        }

        var xmCharge = await context.ChargeCodes.FirstOrDefaultAsync(c => c.Code == "BB-XM", ct);
        if (xmCharge is null)
        {
            return;
        }

        context.ChargeRules.Add(new ChargeRule
        {
            TriggerType = BillingTriggerType.TestVerified,
            TriggerKey = "CXM",
            ChargeCodeId = xmCharge.Id
        });
        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedProductTypesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ProductTypes.AnyAsync(ct))
        {
            return;
        }

        context.ProductTypes.AddRange(
            new ProductType { ProductCode = "RBC-LR", Name = "Red Blood Cells, Leukoreduced", ComponentClass = ComponentClass.RedBloodCells, RequiresCrossmatch = true, RequiresAboMatch = true, RequiresRhMatch = true, DefaultShelfLifeHours = 42 * 24, Isbt128ProductCode = "E0336" },
            new ProductType { ProductCode = "WB", Name = "Whole Blood", ComponentClass = ComponentClass.WholeBlood, RequiresCrossmatch = true, RequiresAboMatch = true, RequiresRhMatch = true, DefaultShelfLifeHours = 35 * 24, Isbt128ProductCode = "E0023" },
            new ProductType { ProductCode = "FFP", Name = "Fresh Frozen Plasma", ComponentClass = ComponentClass.Plasma, RequiresCrossmatch = false, DefaultShelfLifeHours = 365 * 24, Isbt128ProductCode = "E0701" },
            new ProductType { ProductCode = "PLT-A", Name = "Apheresis Platelets", ComponentClass = ComponentClass.Platelets, RequiresCrossmatch = false, DefaultShelfLifeHours = 5 * 24, Isbt128ProductCode = "E3077" });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// RBC and whole blood always require crossmatch; ensure catalog flags and WB product exist.
    /// </summary>
    private static async Task EnsureCellularProductCrossmatchFlagsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var changed = false;

        if (!await context.ProductTypes.AnyAsync(p => p.ProductCode == "WB", ct))
        {
            context.ProductTypes.Add(new ProductType
            {
                ProductCode = "WB",
                Name = "Whole Blood",
                ComponentClass = ComponentClass.WholeBlood,
                RequiresCrossmatch = true,
                RequiresAboMatch = true,
                RequiresRhMatch = true,
                DefaultShelfLifeHours = 35 * 24,
                Isbt128ProductCode = "E0023"
            });
            changed = true;
        }

        var cellular = await context.ProductTypes.Where(p =>
            p.ComponentClass == ComponentClass.RedBloodCells
            || p.ComponentClass == ComponentClass.WholeBlood).ToListAsync(ct);

        foreach (var product in cellular)
        {
            if (!product.RequiresCrossmatch)
            {
                product.RequiresCrossmatch = true;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task SeedProductAttributesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ProductAttributes.AnyAsync(ct))
        {
            return;
        }

        context.ProductAttributes.AddRange(
            new ProductAttribute { Code = "IRRAD", Name = "Irradiated", Description = "Gamma/X-ray irradiated to prevent TA-GVHD" },
            new ProductAttribute { Code = "LR", Name = "Leukoreduced", Description = "White cells reduced by filtration" },
            new ProductAttribute { Code = "CMVNEG", Name = "CMV Negative", Description = "Tested CMV seronegative" },
            new ProductAttribute { Code = "WASHED", Name = "Washed", Description = "Plasma removed by washing" },
            new ProductAttribute { Code = "VOLRED", Name = "Volume Reduced", Description = "Reduced plasma volume" });

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedBloodAttributeDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.BloodAttributeDefinitions.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;

        BloodAttributeDefinition Attr(string code, string name, string antibody, bool significant, int sort) => new()
        {
            Code = code,
            Name = name,
            AntibodyName = antibody,
            IsClinicallySignificant = significant,
            SortOrder = sort,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };

        context.BloodAttributeDefinitions.AddRange(
            Attr("K", "Kell", "anti-K", true, 1),
            Attr("E", "Rh E", "anti-E", true, 2),
            Attr("C", "Rh C", "anti-C", true, 3),
            Attr("c", "Rh c", "anti-c", true, 4),
            Attr("FYA", "Duffy a", "anti-Fya", true, 5),
            Attr("JKA", "Kidd a", "anti-Jka", true, 6),
            Attr("JKB", "Kidd b", "anti-Jkb", true, 7),
            Attr("M", "MNS M", "anti-M", false, 8),
            Attr("N", "MNS N", "anti-N", false, 9));

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedSpecimenTypeDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.SpecimenTypeDefinitions.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;

        SpecimenTypeDefinition Type(string code, string description, int sort, params string[] excludedTests) => new()
        {
            Code = code,
            Description = description,
            ExcludedTestCodesJson = SpecimenTypeExcludedTests.Serialize(excludedTests),
            SortOrder = sort,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };

        context.SpecimenTypeDefinitions.AddRange(
            Type("EDTA", "EDTA Whole Blood", 1),
            Type("SERUM", "Serum", 2, "XM"));

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedSubtestDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.SubtestDefinitions.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var choicesJson = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());

        SubtestDefinition Sub(string code, string name, bool required = true) => new()
        {
            Code = code,
            Name = name,
            ResultType = SubtestResultType.GradedReaction,
            ChoicesJson = choicesJson,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };

        context.SubtestDefinitions.AddRange(
            Sub(AboRhPanelSubtestCodes.AntiA, "Anti-A"),
            Sub(AboRhPanelSubtestCodes.AntiB, "Anti-B"),
            Sub(AboRhPanelSubtestCodes.AntiD, "Anti-D"),
            Sub(AboRhPanelSubtestCodes.ACells, "A cells"),
            Sub(AboRhPanelSubtestCodes.BCells, "B cells"),
            Sub(AboRhPanelSubtestCodes.Control, "Control"),
            Sub(AboRhPanelSubtestCodes.WeakD, "Weak-D"),
            Sub("IS", "Immediate spin"),
            Sub("37C", "37°C"),
            Sub("AHG", "AHG"),
            Sub("CC", "Check cells", required: false),
            Sub("PEG", "PEG", required: false),
            Sub("ENZ", "Enzyme", required: false),
            Sub("Cell1", "Screening cell 1"),
            Sub("Cell2", "Screening cell 2"),
            Sub("Cell3", "Screening cell 3"));

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedPhaseDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.PhaseDefinitions.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.PhaseDefinitions.AddRange(
            Phase("IS", "Immediate spin", 1),
            Phase("37C", "37°C", 2),
            Phase("AHG", "AHG", 3),
            Phase("CC", "Check cells", 4, includeInInterpretation: false, isCheckCell: true, validates: "AHG"));
        await context.SaveChangesAsync(ct);

        PhaseDefinition Phase(string code, string name, int sort, bool includeInInterpretation = true,
            bool isCheckCell = false, string? validates = null) => new()
        {
            Code = code,
            Name = name,
            SortOrder = sort,
            IncludeInInterpretation = includeInInterpretation,
            IsCheckCell = isCheckCell,
            ValidatesPhaseCode = validates,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        };
    }

    private static async Task EnsureCrossmatchSubtestsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var choicesJson = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        var needed = new (string Code, string Name)[]
        {
            ("IS", "Immediate spin"),
            ("37C", "37°C"),
            ("AHG", "AHG"),
            ("CC", "Check cells"),
            ("PEG", "PEG"),
            ("ENZ", "Enzyme")
        };

        var changed = false;
        foreach (var (code, name) in needed)
        {
            if (await context.SubtestDefinitions.AnyAsync(s => s.Code == code, ct))
            {
                continue;
            }

            context.SubtestDefinitions.Add(new SubtestDefinition
            {
                Code = code,
                Name = name,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choicesJson,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Seeds active <see cref="TestDefinition"/>s for the test codes the clinical workflows
    /// already use, so result-entry catalog validation is non-breaking on existing data.
    /// </summary>
    private static async Task SeedTestDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestDefinitions.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;

        TestDefinition Def(string code, string name, TestCategory category, ResultValueType valueType,
            bool aboRh = false, bool antibody = false, bool compatibility = false, string? allowed = null, bool billable = false, string? charge = null)
            => new()
            {
                Code = code,
                Name = name,
                Category = category,
                ResultValueType = valueType,
                AllowedResultValues = allowed,
                VerificationRequired = true,
                ContributesToAboRhHistory = aboRh,
                ContributesToAntibodyHistory = antibody,
                ContributesToCompatibility = compatibility,
                Billable = billable,
                ChargeCodeMapping = charge,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            };

        var aboRh = Def("ABORH", "ABO/Rh Type", TestCategory.AboRh, ResultValueType.AboRh, aboRh: true, compatibility: true, billable: true, charge: "BB-ABORH");
        aboRh.PanelSubtestsJson = PanelSubtestAssignments.ToJson(PanelSubtestDefinitions.DefaultAboRh()
            .Select(s => new PanelSubtestAssignment(s.Code, s.Required, s.SortOrder))
            .ToList());
        aboRh.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(InterpretationLogicDefinitions.DefaultAboRhLogic());

        var xm = Def("XM", "Crossmatch", TestCategory.Crossmatch, ResultValueType.Crossmatch, compatibility: true, allowed: "Compatible\nIncompatible", billable: true, charge: "BB-XM");
        xm.PanelSubtestsJson = DefaultCrossmatchPanelJson();

        var cxm = Def("CXM", "Complex Crossmatch", TestCategory.Crossmatch, ResultValueType.ComplexCrossmatch, compatibility: true, allowed: "Compatible\nIncompatible", billable: true, charge: "BB-XM");
        cxm.PanelSubtestsJson = DefaultComplexCrossmatchPanelJson();

        context.TestDefinitions.AddRange(
            aboRh,
            CreateAntibodyScreenDefinition(now),
            Def("ABID", "Antibody Identification", TestCategory.AntibodyIdentification, ResultValueType.FreeText, antibody: true, compatibility: true),
            xm,
            cxm,
            Def("DAT", "Direct Antiglobulin Test", TestCategory.DirectAntiglobulinTest, ResultValueType.Coded, allowed: "Negative\nPositive"));

        await context.SaveChangesAsync(ct);
    }

    private static string DefaultCrossmatchPanelJson() =>
        PanelSubtestAssignments.ToJson([
            new PanelSubtestAssignment("IS", true, 1),
            new PanelSubtestAssignment("37C", true, 2),
            new PanelSubtestAssignment("AHG", true, 3),
            new PanelSubtestAssignment("CC", false, 4)
        ])!;

    private static string DefaultComplexCrossmatchPanelJson() =>
        PanelSubtestAssignments.ToJson([
            new PanelSubtestAssignment("IS", true, 1),
            new PanelSubtestAssignment("37C", true, 2),
            new PanelSubtestAssignment("AHG", true, 3),
            new PanelSubtestAssignment("PEG", false, 4),
            new PanelSubtestAssignment("ENZ", false, 5),
            new PanelSubtestAssignment("CC", false, 6)
        ])!;

    private static readonly string[] AntibodyScreenCells = ["Cell1", "Cell2", "Cell3"];
    private static readonly string[] AntibodyScreenPhases = ["IS", "37C", "AHG", "CC"];
    private static readonly string[] AntibodyScreenInterpretivePhases = ["IS", "37C", "AHG"];

    private static TestDefinition CreateAntibodyScreenDefinition(DateTime now)
    {
        return new TestDefinition
        {
            Code = "ABSC",
            Name = "Antibody Screen",
            Category = TestCategory.AntibodyScreen,
            ResultValueType = ResultValueType.Subtest,
            VerificationRequired = true,
            ContributesToAntibodyHistory = true,
            ContributesToCompatibility = true,
            Billable = true,
            ChargeCodeMapping = "BB-SCREEN",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1,
            PanelSubtestsJson = DefaultAntibodyScreenPanelJson(),
            InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(
                    AntibodyScreenCells, AntibodyScreenInterpretivePhases))
        };
    }

    private static string DefaultAntibodyScreenPanelJson() =>
        PanelSubtestAssignments.ToJson(
            AntibodyScreenCells.Select((code, i) =>
                new PanelSubtestAssignment(code, true, i + 1, AntibodyScreenPhases)).ToList())!;

    private static async Task EnsureAbscPanelAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await EnsureAntibodyScreenSubtestsAsync(context, ct);
        await EnsurePhaseDefinitionsAsync(context, ct);

        var now = DateTime.UtcNow;
        var absc = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == "ABSC", ct);
        if (absc is null)
        {
            context.TestDefinitions.Add(CreateAntibodyScreenDefinition(now));
            await context.SaveChangesAsync(ct);
            return;
        }

        var assignments = PanelSubtestAssignments.Parse(absc.PanelSubtestsJson);
        var alreadyPhased = assignments.Any(a => a.PhaseCodes is { Count: > 0 });
        if (absc.ResultValueType == ResultValueType.Subtest && alreadyPhased
            && !string.IsNullOrWhiteSpace(absc.InterpretationLogicJson))
        {
            return;
        }

        absc.ResultValueType = ResultValueType.Subtest;
        absc.AllowedResultValues = null;
        absc.PanelSubtestsJson = DefaultAntibodyScreenPanelJson();
        absc.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
            InterpretationLogicDefinitions.DefaultAntibodyScreenLogic(
                AntibodyScreenCells, AntibodyScreenInterpretivePhases));
        await context.SaveChangesAsync(ct);
    }

    private static async Task EnsureAntibodyScreenSubtestsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var choicesJson = SubtestChoiceDefinitions.ToJson(SubtestChoiceDefinitions.DefaultGradedReaction());
        var needed = new (string Code, string Name)[]
        {
            ("Cell1", "Screening cell 1"),
            ("Cell2", "Screening cell 2"),
            ("Cell3", "Screening cell 3")
        };

        var changed = false;
        foreach (var (code, name) in needed)
        {
            if (await context.SubtestDefinitions.AnyAsync(s => s.Code == code, ct))
            {
                continue;
            }

            context.SubtestDefinitions.Add(new SubtestDefinition
            {
                Code = code,
                Name = name,
                ResultType = SubtestResultType.GradedReaction,
                ChoicesJson = choicesJson,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsurePhaseDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var needed = new (string Code, string Name, int Sort, bool Interp, bool Check, string? Validates)[]
        {
            ("IS", "Immediate spin", 1, true, false, null),
            ("37C", "37°C", 2, true, false, null),
            ("AHG", "AHG", 3, true, false, null),
            ("CC", "Check cells", 4, false, true, "AHG")
        };

        var changed = false;
        foreach (var (code, name, sort, interp, check, validates) in needed)
        {
            if (await context.PhaseDefinitions.AnyAsync(p => p.Code == code, ct))
            {
                continue;
            }

            context.PhaseDefinitions.Add(new PhaseDefinition
            {
                Code = code,
                Name = name,
                SortOrder = sort,
                IncludeInInterpretation = interp,
                IsCheckCell = check,
                ValidatesPhaseCode = validates,
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Migrates legacy coded XM to Crossmatch result type with cell panel, and ensures CXM exists.
    /// </summary>
    private static async Task EnsureCrossmatchTestsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await EnsureCrossmatchSubtestsAsync(context, ct);
        await EnsureCxmChargeRuleAsync(context, ct);

        var now = DateTime.UtcNow;
        var changed = false;

        var xm = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == "XM", ct);
        if (xm is not null)
        {
            if (xm.ResultValueType != ResultValueType.Crossmatch)
            {
                xm.ResultValueType = ResultValueType.Crossmatch;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(xm.AllowedResultValues))
            {
                xm.AllowedResultValues = "Compatible\nIncompatible";
                changed = true;
            }

            if (PanelSubtestAssignments.Parse(xm.PanelSubtestsJson).Count == 0)
            {
                xm.PanelSubtestsJson = DefaultCrossmatchPanelJson();
                changed = true;
            }
        }

        if (!await context.TestDefinitions.AnyAsync(t => t.Code == "CXM", ct))
        {
            context.TestDefinitions.Add(new TestDefinition
            {
                Code = "CXM",
                Name = "Complex Crossmatch",
                Category = TestCategory.Crossmatch,
                ResultValueType = ResultValueType.ComplexCrossmatch,
                AllowedResultValues = "Compatible\nIncompatible",
                PanelSubtestsJson = DefaultComplexCrossmatchPanelJson(),
                VerificationRequired = true,
                ContributesToCompatibility = true,
                Billable = true,
                ChargeCodeMapping = "BB-XM",
                IsActive = true,
                IsDraft = false,
                EffectiveUtc = now,
                Version = 1
            });
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    private static async Task EnsureAgtypeTestAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestDefinitions.AnyAsync(t => t.Code == "AGTYPE", ct))
        {
            return;
        }

        var attrs = await context.BloodAttributeDefinitions
            .Where(d => d.IsActive && (d.Code == "K" || d.Code == "FYA"))
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);
        if (attrs.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var scope = BloodAttributeScope.Serialize(attrs.Select(a => new BloodAttributeScopeEntry(a.Code)));

        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "AGTYPE",
            Name = "Antigen Typing Panel",
            Category = TestCategory.AntigenTyping,
            ResultValueType = ResultValueType.BloodAttribute,
            BloodAttributeScopeJson = scope,
            BloodAttributeScopeKind = BloodAttributeKind.Antigen,
            VerificationRequired = true,
            ContributesToCompatibility = true,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Weak D is a standalone reflex test as well as an ABORH panel subtest. The test
    /// definition is required for the seeded Weak D rule to have something to order.
    /// </summary>
    private static async Task EnsureWeakDTestAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestDefinitions.AnyAsync(t => t.Code == "WEAKD", ct))
        {
            return;
        }

        context.TestDefinitions.Add(new TestDefinition
        {
            Code = "WEAKD",
            Name = "Weak D Test",
            Category = TestCategory.AboRh,
            ResultValueType = ResultValueType.Coded,
            AllowedResultValues = "Negative\nPositive",
            VerificationRequired = true,
            ContributesToCompatibility = true,
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = DateTime.UtcNow,
            Version = 1
        });

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds the two reference rules as inactive drafts. They demonstrate both levels of the
    /// engine and are visible in the admin UI, but a site must review and activate them
    /// before they affect any clinical workflow.
    /// </summary>
    private static async Task SeedRuleDefinitionsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.RuleDefinitions.AnyAsync(ct))
        {
            return;
        }

        context.RuleDefinitions.AddRange(
            new RuleDefinition
            {
                Code = "NEO-TYPE-AND-SCREEN",
                Name = "Neonatal type and screen",
                Description =
                    "A patient under one day old receives the neonatal type and screen instead of the standard one.",
                Level = RuleLevel.Order,
                Priority = 100,
                ConditionExpression = "patient.ageDays < 1 AND order.hasTest('TNS')",
                ActionExpression = "cancelTest('TNS'); addTest('TSNEO')",
                IsActive = false,
                IsDraft = true,
                Version = 1
            },
            new RuleDefinition
            {
                Code = "ABORH-RHNEG-WEAKD",
                Name = "Weak D on an Rh negative type",
                Description = "An Rh(D) negative ABO/Rh interpretation reflexes a Weak D test.",
                Level = RuleLevel.Test,
                Priority = 100,
                ConditionExpression =
                    "test.code = 'ABORH' AND test.interpretation IN ('A Negative','B Negative','O Negative','AB Negative')",
                ActionExpression = "addTest('WEAKD')",
                IsActive = false,
                IsDraft = true,
                Version = 1
            });

        await context.SaveChangesAsync(ct);
    }

    private static async Task MigrateExistingTestPanelConfigAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (!await context.SubtestDefinitions.AnyAsync(ct))
        {
            return;
        }

        var aboRh = await context.TestDefinitions.FirstOrDefaultAsync(t => t.Code == "ABORH", ct);
        if (aboRh is null)
        {
            return;
        }

        var assignments = PanelSubtestAssignments.Parse(aboRh.PanelSubtestsJson);
        if (assignments.Count == 0)
        {
            aboRh.PanelSubtestsJson = PanelSubtestAssignments.ToJson(PanelSubtestDefinitions.DefaultAboRh()
                .Select(s => new PanelSubtestAssignment(s.Code, s.Required, s.SortOrder))
                .ToList());
        }

        if (string.IsNullOrWhiteSpace(aboRh.InterpretationLogicJson))
        {
            aboRh.InterpretationLogicJson = InterpretationLogicDefinitions.ToJson(
                InterpretationLogicDefinitions.DefaultAboRhLogic());
        }

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedTestGroupersAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.TestGroupers.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.TestGroupers.Add(new TestGrouper
        {
            Code = "TNS",
            Name = "Type and Screen",
            MemberTestsJson = TestGrouperMembers.ToJson([
                new TestGrouperMember("ABORH", 1),
                new TestGrouperMember("ABSC", 2)
            ]),
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        });

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedReflexRulesAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.ReflexRules.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;
        context.ReflexRules.Add(new ReflexRule
        {
            Code = "ABSC-POS-ABID",
            Name = "Positive antibody screen reflexes antibody identification",
            TriggerTestCode = "ABSC",
            TriggerResultValue = "Positive",
            ReflexTestCode = "ABID",
            IsActive = true,
            IsDraft = false,
            EffectiveUtc = now,
            Version = 1
        });

        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedLocationsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.InventoryLocations,
            (InventoryLocation l) => l.Code,
            [
                new InventoryLocation { Code = "FRIDGE-1", Name = "Main Blood Bank Refrigerator", LocationType = LocationType.Refrigerator },
                new InventoryLocation { Code = "FREEZER-1", Name = "Plasma Freezer", LocationType = LocationType.Freezer },
                new InventoryLocation { Code = "ISSUE", Name = "Issue Window", LocationType = LocationType.Issue }
            ],
            ct);
    }

    private static async Task SeedOrderingLocationsAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.OrderingLocations,
            (OrderingLocation l) => l.Code,
            [
                new OrderingLocation { Code = "OR", Name = "Operating Room", Department = "Surgery", IsActive = true },
                new OrderingLocation { Code = "ICU", Name = "Intensive Care Unit", Department = "Critical Care", IsActive = true },
                new OrderingLocation { Code = "ED", Name = "Emergency Department", Department = "Emergency", IsActive = true },
                new OrderingLocation { Code = "OPLAB", Name = "Outpatient Lab", Department = "Laboratory", IsActive = true },
                new OrderingLocation { Code = "LEGACY", Name = "Legacy Ordering Location", IsActive = true }
            ],
            ct);
    }

    private static async Task SeedOrderingProvidersAsync(BloodBankDbContext context, CancellationToken ct)
    {
        await AddMissingByCodeAsync(
            context,
            context.OrderingProviders,
            (OrderingProvider p) => p.ProviderId,
            [
                new OrderingProvider { ProviderId = "PROV-SMITH", Name = "Dr. Jane Smith", Specialty = "Surgery", Location = "OR", IsActive = true, SourceSystem = "Seed" },
                new OrderingProvider { ProviderId = "PROV-JONES", Name = "Dr. Robert Jones", Specialty = "Hematology", IsActive = true, SourceSystem = "Seed" },
                new OrderingProvider { ProviderId = "PROV-LEE", Name = "Dr. Amy Lee", Location = "ED", IsActive = true, SourceSystem = "Seed" }
            ],
            ct);
    }

    private static async Task AddMissingByCodeAsync<TEntity>(
        BloodBankDbContext context,
        DbSet<TEntity> set,
        Expression<Func<TEntity, string>> codeSelector,
        IReadOnlyList<TEntity> required,
        CancellationToken ct)
        where TEntity : class
    {
        var existingCodes = await set
            .Select(codeSelector)
            .ToListAsync(ct);

        var codeFunc = codeSelector.Compile();
        var existing = existingCodes.ToHashSet(StringComparer.Ordinal);
        var missing = required.Where(e => !existing.Contains(codeFunc(e))).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        set.AddRange(missing);
        await context.SaveChangesAsync(ct);
    }

    private static async Task SeedDemoClinicalDataAsync(BloodBankDbContext context, CancellationToken ct)
    {
        if (await context.Patients.AnyAsync(ct))
        {
            return;
        }

        var rbc = await context.ProductTypes.FirstAsync(t => t.ProductCode == "RBC-LR", ct);
        var plt = await context.ProductTypes.FirstAsync(t => t.ProductCode == "PLT-A", ct);
        var orLoc = await context.OrderingLocations.FirstAsync(l => l.Code == "OR", ct);
        var edLoc = await context.OrderingLocations.FirstAsync(l => l.Code == "ED", ct);
        var smith = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-SMITH", ct);
        var lee = await context.OrderingProviders.FirstOrDefaultAsync(p => p.ProviderId == "PROV-LEE", ct);
        var fridge = await context.InventoryLocations.FirstAsync(l => l.Code == "FRIDGE-1", ct);
        var now = DateTime.UtcNow;

        var patient = new Patient
        {
            MedicalRecordNumber = "MRN0001",
            LastName = "Demo",
            FirstName = "Patricia",
            DateOfBirth = new DateOnly(1980, 4, 12),
            Sex = Sex.Female,
            Status = PatientStatus.Active
        };
        context.Patients.Add(patient);
        await context.SaveChangesAsync(ct);

        var activeVisit = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = "VIS-2026-001",
            EncounterType = EncounterType.Inpatient,
            Status = EncounterStatus.Active,
            AdmitUtc = now.AddDays(-2),
            CurrentLocation = "4W Med/Surg",
            AdmissionLocation = "ED",
            AttendingProviderId = smith?.Id,
            AttendingProvider = smith?.Name ?? "Dr. Smith"
        };
        var priorVisit = new Encounter
        {
            PatientId = patient.Id,
            VisitNumber = "VIS-2025-882",
            EncounterType = EncounterType.Outpatient,
            Status = EncounterStatus.Discharged,
            AdmitUtc = now.AddMonths(-3),
            DischargeUtc = now.AddMonths(-3).AddHours(6),
            CurrentLocation = "Outpatient Clinic"
        };
        context.Encounters.AddRange(activeVisit, priorVisit);
        await context.SaveChangesAsync(ct);

        var specimen = new Specimen
        {
            AccessionNumber = "ACC0001",
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            SpecimenType = "EDTA",
            Barcode = "SPC-ACC0001",
            CollectedUtc = now.AddHours(-2),
            ReceivedUtc = now.AddHours(-1),
            ExpiresUtc = now.AddDays(3),
            Status = SpecimenStatus.Accepted
        };
        context.Specimens.Add(specimen);
        await context.SaveChangesAsync(ct);

        var tsOrder = new Order
        {
            OrderNumber = "ORD0001",
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            OrderingLocationId = orLoc.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Type and Screen",
            OrderType = OrderType.TypeAndScreen,
            TestCode = "TNS",
            Priority = OrderPriority.Routine,
            Status = OrderStatus.InProcess,
            Source = OrderSource.Manual,
            OrderingProviderId = smith?.Id,
            OrderingProvider = smith?.Name,
            OrderedUtc = now.AddHours(-2),
            ResultStatus = ResultStatus.Pending
        };
        var xmOrder = new Order
        {
            OrderNumber = "ORD0002",
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            OrderingLocationId = orLoc.Id,
            OrderCategory = OrderCategory.Test,
            OrderName = "Crossmatch",
            OrderType = OrderType.Crossmatch,
            TestCode = "XM",
            Priority = OrderPriority.Stat,
            Status = OrderStatus.New,
            Source = OrderSource.Manual,
            OrderingProviderId = smith?.Id,
            OrderingProvider = smith?.Name,
            OrderedUtc = now.AddHours(-1)
        };
        var rbcOrder = new Order
        {
            OrderNumber = "ORD0003",
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            OrderingLocationId = edLoc.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Red Blood Cells",
            OrderType = OrderType.Other,
            ProductTypeId = rbc.Id,
            Priority = OrderPriority.Urgent,
            Status = OrderStatus.New,
            Source = OrderSource.Manual,
            OrderingProviderId = lee?.Id,
            OrderingProvider = lee?.Name,
            OrderedUtc = now.AddMinutes(-45),
            FulfillmentStatus = FulfillmentStatus.Ordered
        };
        var pltOrder = new Order
        {
            OrderNumber = "ORD0004",
            PatientId = patient.Id,
            EncounterId = priorVisit.Id,
            OrderingLocationId = orLoc.Id,
            OrderCategory = OrderCategory.Product,
            OrderName = "Platelets",
            OrderType = OrderType.Other,
            ProductTypeId = plt.Id,
            Priority = OrderPriority.Routine,
            Status = OrderStatus.Completed,
            Source = OrderSource.Manual,
            OrderedUtc = now.AddMonths(-3),
            FulfillmentStatus = FulfillmentStatus.Complete
        };
        context.Orders.AddRange(tsOrder, xmOrder, rbcOrder, pltOrder);
        await context.SaveChangesAsync(ct);

        context.OrderLines.AddRange(
            new OrderLine { OrderId = tsOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "ABO/Rh Type", TestCode = "ABORH", OrderType = OrderType.AboRh },
            new OrderLine { OrderId = tsOrder.Id, LineNumber = 2, LineCategory = OrderCategory.Test, LineName = "Antibody Screen", TestCode = "ABSC", OrderType = OrderType.Other },
            new OrderLine { OrderId = xmOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Test, LineName = "Crossmatch", TestCode = "XM", OrderType = OrderType.Crossmatch },
            new OrderLine { OrderId = rbcOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Product, LineName = "Red Blood Cells", ProductTypeId = rbc.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Ordered },
            new OrderLine { OrderId = pltOrder.Id, LineNumber = 1, LineCategory = OrderCategory.Product, LineName = "Platelets", ProductTypeId = plt.Id, OrderType = OrderType.Other, FulfillmentStatus = FulfillmentStatus.Complete });

        context.OrderSpecimens.AddRange(
            new OrderSpecimen { OrderId = tsOrder.Id, SpecimenId = specimen.Id, IsPrimary = true },
            new OrderSpecimen { OrderId = xmOrder.Id, SpecimenId = specimen.Id, IsPrimary = true });

        context.TestResults.Add(new TestResult
        {
            SpecimenId = specimen.Id,
            PatientId = patient.Id,
            OrderId = tsOrder.Id,
            TestCode = "ABORH",
            Version = 1,
            Value = AboRhResultValue.Format(AboGroup.O, RhType.Positive),
            Status = ResultStatus.Verified,
            EnteredBy = "tech1",
            EnteredUtc = now.AddMinutes(-30),
            VerifiedBy = "tech2",
            VerifiedUtc = now.AddMinutes(-20)
        });

        var unit1 = new BloodUnit
        {
            UnitNumber = "W0001230000001",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(30),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.Available,
            CollectionFacility = "Regional Blood Center"
        };
        var unit2 = new BloodUnit
        {
            UnitNumber = "W0001230000002",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Negative,
            ExpiresUtc = now.AddDays(2),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.Quarantine,
            QuarantineReason = "Awaiting infectious disease testing review"
        };
        var unitIssued = new BloodUnit
        {
            UnitNumber = "W0001230000099",
            ProductTypeId = rbc.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            ExpiresUtc = now.AddDays(25),
            CurrentLocationId = fridge.Id,
            Status = UnitStatus.Issued,
            CollectionFacility = "Regional Blood Center"
        };
        context.BloodUnits.AddRange(unit1, unit2, unitIssued);
        await context.SaveChangesAsync(ct);

        context.PatientBloodTypeHistory.Add(new PatientBloodTypeHistory
        {
            PatientId = patient.Id,
            Abo = AboGroup.O,
            RhD = RhType.Positive,
            Source = BloodTypeSource.TestResult,
            IsCurrent = true
        });

        var allocation = new Allocation
        {
            BloodProductId = unit1.Id,
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            OrderId = rbcOrder.Id,
            SpecimenId = specimen.Id,
            Status = AllocationStatus.Reserved,
            AllocatedUtc = now.AddMinutes(-30),
            AllocatedBy = "tech1",
            ExpiresUtc = now.AddHours(24)
        };
        context.Allocations.Add(allocation);

        context.Crossmatches.Add(new Crossmatch
        {
            BloodProductId = unit1.Id,
            PatientId = patient.Id,
            SpecimenId = specimen.Id,
            Method = CrossmatchMethod.Serologic,
            Result = CrossmatchResult.Compatible,
            PerformedUtc = now.AddMinutes(-25),
            PerformedBy = "tech1",
            ExpiresUtc = specimen.ExpiresUtc
        });

        var issue = new Issue
        {
            BloodProductId = unitIssued.Id,
            PatientId = patient.Id,
            EncounterId = activeVisit.Id,
            OrderId = rbcOrder.Id,
            IssuedUtc = now.AddMinutes(-20),
            IssuedBy = "tech2",
            IssuedTo = "Patricia Demo",
            IssuedToLocation = "4W Med/Surg",
            Status = IssueStatus.Issued
        };
        context.Issues.Add(issue);
        await context.SaveChangesAsync(ct);

        context.TransfusionEvents.Add(new TransfusionEvent
        {
            IssueId = issue.Id,
            BloodProductId = unitIssued.Id,
            PatientId = patient.Id,
            StartUtc = now.AddMinutes(-15),
            StopUtc = now.AddMinutes(-5),
            VolumeTransfused = 250m,
            Transfusionist = "RN Jones",
            ReactionSuspected = false,
            FinalDisposition = TransfusionDisposition.Completed,
            DocumentedBy = "tech2"
        });

        await context.SaveChangesAsync(ct);
    }
}
