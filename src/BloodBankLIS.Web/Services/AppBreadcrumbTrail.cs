namespace BloodBankLIS.Web.Services;

/// <summary>Maps the current Blazor route to a teaching breadcrumb trail.</summary>
public static class AppBreadcrumbTrail
{
    public sealed record Crumb(string Label, string? Href);

    private static readonly Dictionary<string, string> Exact = new(StringComparer.OrdinalIgnoreCase)
    {
        ["patients"] = "Patients",
        ["test-worklist"] = "Test Worklist",
        ["antibody-id"] = "Antibody ID",
        ["inventory"] = "Inventory",
        ["compatibility"] = "Compatibility",
        ["issuing"] = "Issue & Transfuse",
        ["lookback"] = "Lookback",
        ["reactions"] = "Reactions",
        ["deviations"] = "Deviations",
        ["printing"] = "Printing",
        ["billing"] = "Billing",
        ["hl7"] = "HL7 Interface",
        ["audit"] = "Audit Trail",
        ["downtime"] = "Downtime Snapshot",
        ["admin"] = "Administration"
    };

    private static readonly Dictionary<string, string> AdminPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["antibody-panel-lots"] = "Antibody Panel Lots",
        ["blood-attributes"] = "Blood Attributes",
        ["charge-codes"] = "Charge Codes",
        ["locations"] = "Ordering Locations",
        ["phases"] = "Phases",
        ["product-billing"] = "Product Billing",
        ["products"] = "Products",
        ["providers"] = "Providers",
        ["special-requirements"] = "Special Requirements",
        ["specimen-types"] = "Specimen Types",
        ["subtests"] = "Subtests",
        ["test-groupers"] = "Test Groupers",
        ["test-service-billing"] = "Test/Service Billing",
        ["tests"] = "Tests",
        ["charge-rules"] = "Charge Rules",
        ["compatibility-rules"] = "Compatibility Tables",
        ["crossmatch-settings"] = "Crossmatch Settings",
        ["exceptions"] = "Exceptions",
        ["facility-policies"] = "Facility Policy",
        ["modification-rules"] = "Modification Rules",
        ["rules"] = "Order & Test Rules",
        ["reflex-rules"] = "Reflex Rules",
        ["expiration-codes"] = "Expiration Codes",
        ["inventory-locations"] = "Inventory Locations",
        ["isbt-product-codes"] = "ISBT Product Codes",
        ["hl7"] = "Interface Setup",
        ["translations"] = "Translations",
        ["history"] = "Config History",
        ["roles"] = "Roles",
        ["users"] = "Users"
    };

    private static readonly Dictionary<string, string> PatientTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["overview"] = "Overview",
        ["visits"] = "Visits",
        ["orders"] = "Orders",
        ["specimens"] = "Specimens",
        ["tests"] = "Tests",
        ["test-history"] = "Test History",
        ["products"] = "Products",
        ["history"] = "Product History"
    };

    public static IReadOnlyList<Crumb> FromRelative(string relativeUri)
    {
        var qIndex = relativeUri.IndexOf('?', StringComparison.Ordinal);
        var path = (qIndex >= 0 ? relativeUri[..qIndex] : relativeUri).Trim('/');
        var query = qIndex >= 0 ? relativeUri[(qIndex + 1)..] : string.Empty;
        var qs = ParseQuery(query);
        var crumbs = new List<Crumb>();

        if (string.IsNullOrEmpty(path))
        {
            crumbs.Add(new("Dashboard", null));
            return crumbs;
        }

        crumbs.Add(new("Dashboard", "/"));

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts[0].Equals("patients", StringComparison.OrdinalIgnoreCase))
        {
            crumbs.Add(new("Patients", "/patients"));
            if (parts.Length >= 2 && long.TryParse(parts[1], out _))
            {
                var chartHref = $"/patients/{parts[1]}";
                crumbs.Add(new("Chart", chartHref));
                if (parts.Length >= 3 && parts[2].Equals("antibody-id", StringComparison.OrdinalIgnoreCase))
                {
                    crumbs.Add(new("Antibody ID", $"{chartHref}/antibody-id"));
                    if (parts.Length >= 4)
                    {
                        crumbs.Add(new("Workup", null));
                    }
                }
                else if (qs.TryGetValue("tab", out var tab) && PatientTabs.TryGetValue(tab, out var tabLabel))
                {
                    crumbs.Add(new(tabLabel, null));
                    if (tab.Equals("orders", StringComparison.OrdinalIgnoreCase)
                        && qs.TryGetValue("panel", out var panel)
                        && panel.Equals("accession", StringComparison.OrdinalIgnoreCase))
                    {
                        crumbs.Add(new("Accession", null));
                    }
                }
            }

            return Finalize(crumbs);
        }

        if (parts[0].Equals("inventory", StringComparison.OrdinalIgnoreCase))
        {
            crumbs.Add(new("Inventory", "/inventory"));
            if (parts.Length >= 2 && parts[1].Equals("isbt-receive", StringComparison.OrdinalIgnoreCase))
            {
                crumbs.Add(new("ISBT Receive", null));
            }
            else if (parts.Length >= 2 && parts[1].Equals("modify", StringComparison.OrdinalIgnoreCase))
            {
                crumbs.Add(new("Modify Products", null));
            }
            else if (qs.TryGetValue("board", out var board) && board.Equals("retype", StringComparison.OrdinalIgnoreCase))
            {
                crumbs.Add(new("Retype desk", null));
            }
            else if (qs.ContainsKey("unitId"))
            {
                crumbs.Add(new("Manage unit", null));
            }

            return Finalize(crumbs);
        }

        if (parts[0].Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            crumbs.Add(new("Administration", "/admin"));
            for (var i = 1; i < parts.Length; i++)
            {
                var label = AdminPages.TryGetValue(parts[i], out var mapped) ? mapped : TitleCase(parts[i]);
                var href = i < parts.Length - 1 ? "/" + string.Join('/', parts.Take(i + 1)) : null;
                crumbs.Add(new(label, href));
            }

            return Finalize(crumbs);
        }

        if (Exact.TryGetValue(parts[0], out var exact))
        {
            if (qs.TryGetValue("patientId", out var pid) && long.TryParse(pid, out var patientId))
            {
                crumbs.Add(new("Patients", "/patients"));
                crumbs.Add(new("Chart", $"/patients/{patientId}"));
            }

            crumbs.Add(new(exact, null));
            if (parts[0].Equals("compatibility", StringComparison.OrdinalIgnoreCase)
                && qs.TryGetValue("from", out var from)
                && from.Equals("retro", StringComparison.OrdinalIgnoreCase))
            {
                crumbs.Add(new("Emergency / MTP", null));
            }
            else if (parts[0].Equals("printing", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("issueId"))
            {
                crumbs.Add(new("Issue reprint", null));
            }
            else if (parts[0].Equals("printing", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("specimenId"))
            {
                crumbs.Add(new("Specimen label", null));
            }
            else if (parts[0].Equals("printing", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("unitId"))
            {
                crumbs.Add(new("Component label", null));
            }
            else if (parts[0].Equals("issuing", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("bloodUnitId"))
            {
                crumbs.Add(new("Issue unit", null));
            }
            else if (parts[0].Equals("lookback", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("mrn"))
            {
                crumbs.Add(new("Recipient traceback", null));
            }
            else if (parts[0].Equals("reactions", StringComparison.OrdinalIgnoreCase)
                     && qs.ContainsKey("id"))
            {
                crumbs.Add(new("Investigation", null));
            }

            return Finalize(crumbs);
        }

        crumbs.Add(new(TitleCase(parts[^1]), null));
        return Finalize(crumbs);
    }

    private static IReadOnlyList<Crumb> Finalize(List<Crumb> crumbs)
    {
        if (crumbs.Count == 0)
        {
            return crumbs;
        }

        var last = crumbs[^1];
        crumbs[^1] = last with { Href = null };
        return crumbs;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return map;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            map[key] = value;
        }

        return map;
    }

    private static string TitleCase(string slug) =>
        string.Join(' ', slug.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w[1..]));
}
