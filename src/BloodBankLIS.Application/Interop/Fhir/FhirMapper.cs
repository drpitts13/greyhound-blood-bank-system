using System.Text.Json.Nodes;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Entities.Configuration;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Interop.Fhir;

/// <summary>
/// Read-only FHIR R4 JSON for the EHR (OCD-043). Coded fields use this system's own code systems with readable
/// text; mapping to LOINC or SNOMED CT is an interface decision and REQUIRES INTERFACE / SME VERIFICATION, so no
/// external codes are asserted here.
/// </summary>
public static class FhirMapper
{
    public const string MrnSystem = "urn:hemocore:mrn";
    public const string OrderNumberSystem = "urn:hemocore:order-number";
    public const string UnitNumberSystem = "urn:hemocore:unit-number";
    public const string DinSystem = "urn:hemocore:isbt128-din";
    public const string ObservationCodeSystem = "urn:hemocore:observation";
    public const string FlagCodeSystem = "urn:hemocore:transfusion-requirement";
    public const string LoincSystem = "http://loinc.org";
    public const string InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation";
    public const string OrderCodeSystem = "urn:hemocore:order";
    public const string DiagnosticSectionSystem = "http://terminology.hl7.org/CodeSystem/v2-0074";

    /// <summary>
    /// One order's verified results as a blood bank report: partial while any test on the order has no verified
    /// result, final when all have, corrected when a result was corrected after verification.
    /// </summary>
    public static JsonObject DiagnosticReport(Order o, IReadOnlyList<TestResult> verified, bool complete)
    {
        var code = string.IsNullOrWhiteSpace(o.TestCode)
            ? new JsonObject { ["text"] = string.IsNullOrWhiteSpace(o.OrderName) ? "Blood bank order" : o.OrderName }
            : Concept(OrderCodeSystem, o.TestCode, string.IsNullOrWhiteSpace(o.OrderName) ? o.TestCode : o.OrderName);
        var resource = new JsonObject
        {
            ["resourceType"] = "DiagnosticReport",
            ["id"] = o.Id.ToString(),
            ["basedOn"] = new JsonArray(Reference("ServiceRequest", o.Id)),
            ["status"] = !complete ? "partial" : verified.Any(r => r.Status == ResultStatus.Corrected) ? "corrected" : "final",
            ["category"] = new JsonArray(Concept(DiagnosticSectionSystem, "BLB", "Blood Bank")),
            ["code"] = code,
            ["subject"] = Reference("Patient", o.PatientId),
            ["result"] = new JsonArray(verified.OrderBy(r => r.TestCode).ThenBy(r => r.Id)
                .Select(r => (JsonNode)Reference("Observation", "result-" + r.Id)).ToArray())
        };
        if (verified.Where(r => r.EnteredUtc is not null).Select(r => r.EnteredUtc!.Value).DefaultIfEmpty().Min() is var first && first != default)
        {
            resource["effectiveDateTime"] = Instant(first);
        }

        if (verified.Where(r => r.VerifiedUtc is not null).Select(r => r.VerifiedUtc!.Value).DefaultIfEmpty().Max() is var issued && issued != default)
        {
            resource["issued"] = Instant(issued);
        }

        return resource;
    }

    /// <summary>
    /// A verified test result as a laboratory Observation: the local test code, plus its LOINC code when the test has
    /// one; a numeric value as a quantity with units, reference range, and abnormal flag; otherwise the value as text.
    /// Status follows the result: final, corrected (a verified correction), or entered-in-error (invalidated).
    /// </summary>
    public static JsonObject TestResult(TestResult r, TestDefinition? definition)
    {
        var name = string.IsNullOrWhiteSpace(definition?.Name) ? r.TestCode : definition.Name;
        var code = Concept(ObservationCodeSystem, r.TestCode, name);
        if (!string.IsNullOrWhiteSpace(definition?.LoincCode))
        {
            code["coding"]!.AsArray().Add(new JsonObject { ["system"] = LoincSystem, ["code"] = definition.LoincCode.Trim(), ["display"] = name });
        }

        var resource = new JsonObject
        {
            ["resourceType"] = "Observation",
            ["id"] = "result-" + r.Id,
            ["status"] = r.Status switch
            {
                ResultStatus.Invalidated => "entered-in-error",
                ResultStatus.Corrected => "corrected",
                _ => "final"
            },
            ["category"] = new JsonArray(Concept("http://terminology.hl7.org/CodeSystem/observation-category", "laboratory", "Laboratory")),
            ["code"] = code,
            ["subject"] = Reference("Patient", r.PatientId)
        };
        if (r.OrderId is long orderId)
        {
            resource["basedOn"] = new JsonArray(Reference("ServiceRequest", orderId));
        }

        if (r.EnteredUtc is DateTime entered)
        {
            resource["effectiveDateTime"] = Instant(entered);
        }

        if (r.VerifiedUtc is DateTime verified)
        {
            resource["issued"] = Instant(verified);
        }

        var numeric = definition?.ResultValueType == ResultValueType.Numeric
            && decimal.TryParse(r.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _);
        if (numeric)
        {
            var quantity = new JsonObject { ["value"] = decimal.Parse(r.Value!, System.Globalization.CultureInfo.InvariantCulture) };
            var units = r.Units ?? definition!.ResultUnits;
            if (!string.IsNullOrWhiteSpace(units))
            {
                quantity["unit"] = units;
            }

            resource["valueQuantity"] = quantity;
            if (definition!.ReferenceLow is not null || definition.ReferenceHigh is not null)
            {
                var range = new JsonObject();
                if (definition.ReferenceLow is decimal low) range["low"] = new JsonObject { ["value"] = low };
                if (definition.ReferenceHigh is decimal high) range["high"] = new JsonObject { ["value"] = high };
                resource["referenceRange"] = new JsonArray(range);
            }
        }
        else if (!string.IsNullOrWhiteSpace(r.Value))
        {
            // An ABO/Rh result reads as the chart shows it ("O+"), not its stored form.
            resource["valueString"] = definition?.ResultValueType == ResultValueType.AboRh
                || r.TestCode.StartsWith("ABORH", StringComparison.OrdinalIgnoreCase)
                    ? Domain.ValueObjects.AboRhResultValue.FormatDisplay(r.Value)
                    : Results.ResultValueText.For(r.Value, r.Units);
        }

        if (!string.IsNullOrWhiteSpace(r.AbnormalFlag))
        {
            resource["interpretation"] = new JsonArray(Concept(InterpretationSystem, r.AbnormalFlag.Trim(), null));
        }
        else if (!string.IsNullOrWhiteSpace(r.Interpretation) && !numeric)
        {
            resource["interpretation"] = new JsonArray(new JsonObject { ["text"] = r.Interpretation });
        }

        return resource;
    }

    public static JsonObject Patient(Patient p, Patient? survivor = null)
    {
        var name = new JsonObject { ["family"] = p.LastName, ["given"] = Given(p) };
        var resource = new JsonObject
        {
            ["resourceType"] = "Patient",
            ["id"] = p.Id.ToString(),
            ["identifier"] = new JsonArray(Identifier(MrnSystem, p.MedicalRecordNumber, "MR")),
            ["active"] = p.Status == PatientStatus.Active,
            ["name"] = new JsonArray(name),
            ["gender"] = p.Sex switch
            {
                Sex.Male => "male",
                Sex.Female => "female",
                Sex.Other => "other",
                _ => "unknown"
            },
            ["birthDate"] = p.DateOfBirth.ToString("yyyy-MM-dd")
        };

        if (p.Deceased)
        {
            resource["deceasedBoolean"] = true;
        }

        if (survivor is not null)
        {
            resource["link"] = new JsonArray(new JsonObject
            {
                ["other"] = Reference("Patient", survivor.Id),
                ["type"] = "replaced-by"
            });
        }

        return resource;
    }

    public static JsonObject BloodType(PatientBloodTypeHistory b) => new()
    {
        ["resourceType"] = "Observation",
        ["id"] = "abo-" + b.Id,
        ["status"] = "final",
        ["category"] = new JsonArray(Concept("http://terminology.hl7.org/CodeSystem/observation-category", "laboratory", "Laboratory")),
        ["code"] = Concept(ObservationCodeSystem, "ABORH", "ABO/Rh group"),
        ["subject"] = Reference("Patient", b.PatientId),
        ["effectiveDateTime"] = Instant(b.CreatedUtc),
        ["valueCodeableConcept"] = new JsonObject { ["text"] = $"{b.Abo} {RhText(b.RhD)}" }
    };

    public static JsonObject Antibody(AntibodyHistory a)
    {
        var resource = new JsonObject
        {
            ["resourceType"] = "Observation",
            ["id"] = "antibody-" + a.Id,
            ["status"] = "final",
            ["category"] = new JsonArray(Concept("http://terminology.hl7.org/CodeSystem/observation-category", "laboratory", "Laboratory")),
            ["code"] = Concept(ObservationCodeSystem, "ANTIBODY", "Red cell antibody identified"),
            ["subject"] = Reference("Patient", a.PatientId),
            ["effectiveDateTime"] = Instant(a.CreatedUtc),
            ["valueString"] = a.AntibodySpecificity
        };

        // FHIR does not allow null values, so optional elements are added only when present.
        if (!string.IsNullOrWhiteSpace(a.Comment))
        {
            resource["note"] = new JsonArray(new JsonObject { ["text"] = a.Comment });
        }

        return resource;
    }

    public static JsonObject Requirement(SpecialTransfusionRequirement r, string? definitionName)
    {
        var text = !string.IsNullOrWhiteSpace(definitionName)
            ? definitionName
            : r.RequirementType == SpecialTransfusionRequirementType.AntigenNegative && !string.IsNullOrWhiteSpace(r.AntigenCode)
                ? $"{r.AntigenCode}-negative blood"
                : r.RequirementType switch
                {
                    SpecialTransfusionRequirementType.Irradiated => "Irradiated blood",
                    SpecialTransfusionRequirementType.CmvNegative => "CMV-negative blood",
                    SpecialTransfusionRequirementType.Leukoreduced => "Leukoreduced blood",
                    SpecialTransfusionRequirementType.Washed => "Washed blood",
                    _ => "Special transfusion requirement"
                };
        var period = new JsonObject { ["start"] = Instant(r.EffectiveUtc) };
        if (r.ExpiresUtc is DateTime end)
        {
            period["end"] = Instant(end);
        }

        return new JsonObject
        {
            ["resourceType"] = "Flag",
            ["id"] = "requirement-" + r.Id,
            ["status"] = r.IsActive ? "active" : "inactive",
            ["code"] = Concept(FlagCodeSystem, r.RequirementType.ToString(), text),
            ["subject"] = Reference("Patient", r.PatientId),
            ["period"] = period
        };
    }

    public static JsonObject ServiceRequest(Order o) => new()
    {
        ["resourceType"] = "ServiceRequest",
        ["id"] = o.Id.ToString(),
        ["identifier"] = new JsonArray(Identifier(OrderNumberSystem, o.OrderNumber, "PLAC")),
        ["status"] = o.Status switch
        {
            OrderStatus.Completed => "completed",
            OrderStatus.Cancelled or OrderStatus.Discontinued => "revoked",
            OrderStatus.OnHold => "on-hold",
            _ => "active"
        },
        ["intent"] = "order",
        ["priority"] = o.Priority switch
        {
            OrderPriority.Stat or OrderPriority.EmergencyRelease or OrderPriority.MassiveTransfusionProtocol => "stat",
            OrderPriority.Urgent => "urgent",
            _ => "routine"
        },
        ["code"] = new JsonObject { ["text"] = string.IsNullOrWhiteSpace(o.OrderName) ? o.TestCode ?? "Order" : o.OrderName },
        ["subject"] = Reference("Patient", o.PatientId),
        ["authoredOn"] = Instant(o.OrderedUtc)
    };

    public static JsonObject Product(BloodUnit u, ProductType? type)
    {
        var identifiers = new JsonArray(Identifier(UnitNumberSystem, u.UnitNumber, null));
        if (!string.IsNullOrWhiteSpace(u.Din))
        {
            identifiers.Add(Identifier(DinSystem, u.Din, null));
        }

        return new JsonObject
        {
            ["resourceType"] = "BiologicallyDerivedProduct",
            ["id"] = u.Id.ToString(),
            ["identifier"] = identifiers,
            ["productCategory"] = type?.ComponentClass is ComponentClass.Plasma or ComponentClass.Cryoprecipitate or ComponentClass.WholeBlood ? "fluid" : "cells",
            ["productCode"] = new JsonObject { ["text"] = type?.Name ?? $"Product {u.ProductTypeId}" },
            ["status"] = u.Status == UnitStatus.Available ? "available" : "unavailable",
            ["storage"] = new JsonArray(new JsonObject
            {
                ["description"] = $"{u.Abo} {RhText(u.RhD)}; status {u.Status}",
                ["duration"] = new JsonObject { ["end"] = Instant(u.ExpiresUtc) }
            })
        };
    }

    public static JsonObject Bundle(IEnumerable<JsonObject> resources, string selfUrl)
    {
        var entries = new JsonArray();
        foreach (var r in resources)
        {
            entries.Add(new JsonObject
            {
                ["fullUrl"] = $"{r["resourceType"]}/{r["id"]}",
                ["resource"] = r,
                ["search"] = new JsonObject { ["mode"] = "match" }
            });
        }

        return new JsonObject
        {
            ["resourceType"] = "Bundle",
            ["type"] = "searchset",
            ["total"] = entries.Count,
            ["link"] = new JsonArray(new JsonObject { ["relation"] = "self", ["url"] = selfUrl }),
            ["entry"] = entries
        };
    }

    public static JsonObject Outcome(string code, string message) => new()
    {
        ["resourceType"] = "OperationOutcome",
        ["issue"] = new JsonArray(new JsonObject
        {
            ["severity"] = "error",
            ["code"] = code,
            ["diagnostics"] = message
        })
    };

    public static JsonObject Capability(DateTime nowUtc)
    {
        JsonObject Read(string type, params string[] searchParams)
        {
            var resource = new JsonObject
            {
                ["type"] = type,
                ["interaction"] = new JsonArray(new JsonObject { ["code"] = "read" }, new JsonObject { ["code"] = "search-type" })
            };
            if (searchParams.Length > 0)
            {
                var array = new JsonArray();
                foreach (var name in searchParams)
                {
                    array.Add(new JsonObject { ["name"] = name, ["type"] = name == "patient" ? "reference" : "token" });
                }

                resource["searchParam"] = array;
            }

            return resource;
        }

        return new JsonObject
        {
            ["resourceType"] = "CapabilityStatement",
            ["status"] = "active",
            ["date"] = Instant(nowUtc),
            ["kind"] = "instance",
            ["fhirVersion"] = "4.0.1",
            ["format"] = new JsonArray("application/fhir+json"),
            ["description"] = "Read-only blood bank data for the EHR. No create, update, or delete. Codes use local systems; external terminology mapping requires interface/SME verification (OCD-043).",
            ["rest"] = new JsonArray(new JsonObject
            {
                ["mode"] = "server",
                ["resource"] = new JsonArray(
                    Read("Patient", "identifier"),
                    Read("Observation", "patient"),
                    Read("Flag", "patient"),
                    Read("ServiceRequest", "patient"),
                    Read("DiagnosticReport", "patient"),
                    Read("BiologicallyDerivedProduct", "identifier"))
            })
        };
    }

    public static JsonObject Reference(string type, long id) => new() { ["reference"] = $"{type}/{id}" };

    public static JsonObject Reference(string type, string id) => new() { ["reference"] = $"{type}/{id}" };

    private static JsonArray Given(Patient p)
    {
        var given = new JsonArray(p.FirstName);
        if (!string.IsNullOrWhiteSpace(p.MiddleName))
        {
            given.Add(p.MiddleName);
        }

        return given;
    }

    private static JsonObject Identifier(string system, string value, string? typeCode)
    {
        var id = new JsonObject { ["system"] = system, ["value"] = value };
        if (typeCode is not null)
        {
            id["type"] = Concept("http://terminology.hl7.org/CodeSystem/v2-0203", typeCode, null);
        }

        return id;
    }

    private static JsonObject Concept(string system, string code, string? display)
    {
        var coding = new JsonObject { ["system"] = system, ["code"] = code };
        if (display is not null)
        {
            coding["display"] = display;
        }

        var concept = new JsonObject { ["coding"] = new JsonArray(coding) };
        if (display is not null)
        {
            concept["text"] = display;
        }

        return concept;
    }

    private static string Instant(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string RhText(RhType rh) => rh switch
    {
        RhType.Positive => "Pos",
        RhType.Negative => "Neg",
        _ => "Rh unknown"
    };
}
