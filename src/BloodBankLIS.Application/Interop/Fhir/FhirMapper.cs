using System.Text.Json.Nodes;
using BloodBankLIS.Domain.Entities;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Application.Interop.Fhir;

/// <summary>
/// Read-only FHIR R4 JSON for the EHR (OCD-043). Coded fields use this system's own code systems with readable
/// text; mapping to LOINC or SNOMED CT is an interface decision and REQUIRES INTERFACE / SME VERIFICATION, so no
/// external codes are asserted here.
/// </summary>
public static class FhirMapper
{
    public const string MrnSystem = "urn:greyhound-lis:mrn";
    public const string OrderNumberSystem = "urn:greyhound-lis:order-number";
    public const string UnitNumberSystem = "urn:greyhound-lis:unit-number";
    public const string DinSystem = "urn:greyhound-lis:isbt128-din";
    public const string ObservationCodeSystem = "urn:greyhound-lis:observation";
    public const string FlagCodeSystem = "urn:greyhound-lis:transfusion-requirement";

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
                    Read("BiologicallyDerivedProduct", "identifier"))
            })
        };
    }

    public static JsonObject Reference(string type, long id) => new() { ["reference"] = $"{type}/{id}" };

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
