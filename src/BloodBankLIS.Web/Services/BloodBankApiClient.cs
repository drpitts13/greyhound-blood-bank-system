using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BloodBankLIS.Application.Admin;
using BloodBankLIS.Application.Billing;
using BloodBankLIS.Application.Compatibility;
using BloodBankLIS.Application.Compliance;
using BloodBankLIS.Application.Immunohematology;
using BloodBankLIS.Application.Inventory;
using BloodBankLIS.Application.Isbt128;
using BloodBankLIS.Application.Issuing;
using BloodBankLIS.Application.Modifications;
using BloodBankLIS.Application.PatientWorkspace;
using BloodBankLIS.Application.Patients;
using BloodBankLIS.Application.Reference;
using BloodBankLIS.Application.Results;
using BloodBankLIS.Application.Specimens;
using BloodBankLIS.Domain.Enums;

namespace BloodBankLIS.Web.Services;

/// <summary>
/// Typed client over the Blood Bank HTTP API. Every method returns an
/// <see cref="ApiResult{T}"/> that normalizes success, non-blocking warnings, safety
/// gate blocks (422), and auth/validation failures so the UI handles them uniformly.
/// </summary>
public sealed class BloodBankApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public BloodBankApiClient(HttpClient http)
    {
        _http = http;
    }

    // ---- Identity ----
    public Task<ApiResult<MeVm>> GetMeAsync(CancellationToken ct = default) =>
        SendAsync<MeVm>(HttpMethod.Get, "api/me", ct: ct);

    public Task<ApiResult<MeVm>> LoginAsync(LoginRequestVm req, CancellationToken ct = default) =>
        SendAsync<MeVm>(HttpMethod.Post, "api/auth/login", req, ct);

    public Task<ApiResult<object>> LogoutAsync(CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, "api/auth/logout", ct: ct);

    // ---- Patients ----
    public Task<ApiResult<List<PatientDto>>> GetPatientsAsync(CancellationToken ct = default) =>
        SendAsync<List<PatientDto>>(HttpMethod.Get, "api/patients", ct: ct);

    public Task<ApiResult<PatientDto>> GetPatientAsync(long id, CancellationToken ct = default) =>
        SendAsync<PatientDto>(HttpMethod.Get, $"api/patients/{id}", ct: ct);

    public Task<ApiResult<PatientDto>> CreatePatientAsync(CreatePatientRequest req, CancellationToken ct = default) =>
        SendAsync<PatientDto>(HttpMethod.Post, "api/patients", req, ct);

    public Task<ApiResult<PatientDto>> UpdatePatientAsync(long id, UpdatePatientRequest req, CancellationToken ct = default) =>
        SendAsync<PatientDto>(HttpMethod.Put, $"api/patients/{id}", req, ct);

    // ---- Patient workspace ----
    public Task<ApiResult<List<EncounterDto>>> GetPatientEncountersAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<EncounterDto>>(HttpMethod.Get, $"api/patients/{patientId}/encounters", ct: ct);

    public Task<ApiResult<List<PatientOrderDto>>> GetPatientOrdersAsync(
        long patientId,
        long? encounterId = null,
        string? category = null,
        bool? activeOnly = null,
        string? q = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (encounterId.HasValue) query.Add($"encounterId={encounterId}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        if (activeOnly.HasValue) query.Add($"activeOnly={activeOnly.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return SendAsync<List<PatientOrderDto>>(HttpMethod.Get, $"api/patients/{patientId}/orders{qs}", ct: ct);
    }

    public Task<ApiResult<List<PatientProductHistoryRowDto>>> GetPatientProductHistoryAsync(
        long patientId,
        long? encounterId = null,
        CancellationToken ct = default)
    {
        var qs = encounterId.HasValue ? $"?encounterId={encounterId}" : string.Empty;
        return SendAsync<List<PatientProductHistoryRowDto>>(HttpMethod.Get, $"api/patients/{patientId}/product-history{qs}", ct: ct);
    }

    public Task<ApiResult<List<PatientTestHistoryRowDto>>> GetPatientTestHistoryAsync(
        long patientId,
        CancellationToken ct = default) =>
        SendAsync<List<PatientTestHistoryRowDto>>(HttpMethod.Get, $"api/patients/{patientId}/test-history", ct: ct);

    public Task<ApiResult<List<PatientAllocationRowDto>>> GetPatientAllocationsAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<PatientAllocationRowDto>>(HttpMethod.Get, $"api/patients/{patientId}/allocations", ct: ct);

    public Task<ApiResult<List<CompatibleUnitDto>>> GetPatientCompatibleUnitsAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<CompatibleUnitDto>>(HttpMethod.Get, $"api/patients/{patientId}/compatible-units", ct: ct);

    public Task<ApiResult<List<CrossmatchTestOptionDto>>> GetPatientCrossmatchTestsAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<CrossmatchTestOptionDto>>(HttpMethod.Get, $"api/patients/{patientId}/crossmatch-tests", ct: ct);

    public Task<ApiResult<AllocatePatientUnitResultDto>> AllocatePatientUnitAsync(
        long patientId,
        AllocatePatientUnitRequest req,
        CancellationToken ct = default) =>
        SendAsync<AllocatePatientUnitResultDto>(HttpMethod.Post, $"api/patients/{patientId}/allocations", req, ct);

    public Task<ApiResult<EncounterDto>> CreatePatientEncounterAsync(long patientId, CreateEncounterRequest req, CancellationToken ct = default) =>
        SendAsync<EncounterDto>(HttpMethod.Post, $"api/patients/{patientId}/encounters", req, ct);

    public Task<ApiResult<object>> CreatePatientOrderAsync(long patientId, CreateOrderRequest req, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, $"api/patients/{patientId}/orders", req, ct);

    public Task<ApiResult<object>> UpdatePatientOrderAsync(long patientId, long orderId, UpdateOrderRequest req, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Put, $"api/patients/{patientId}/orders/{orderId}", req, ct);

    public Task<ApiResult<PatientOrderDto>> LinkPatientOrderSpecimenAsync(
        long patientId,
        long orderId,
        LinkOrderSpecimenRequest req,
        CancellationToken ct = default) =>
        SendAsync<PatientOrderDto>(HttpMethod.Put, $"api/patients/{patientId}/orders/{orderId}/specimen", req, ct);

    // ---- Specimens ----
    public Task<ApiResult<SpecimenDto>> AccessionSpecimenAsync(AccessionSpecimenRequest req, CancellationToken ct = default) =>
        SendAsync<SpecimenDto>(HttpMethod.Post, "api/specimens", req, ct);

    public Task<ApiResult<SpecimenDto>> UpdateSpecimenAsync(long id, UpdateSpecimenRequest req, CancellationToken ct = default) =>
        SendAsync<SpecimenDto>(HttpMethod.Put, $"api/specimens/{id}", req, ct);

    public Task<ApiResult<SpecimenDto>> GetSpecimenAsync(long id, CancellationToken ct = default) =>
        SendAsync<SpecimenDto>(HttpMethod.Get, $"api/specimens/{id}", ct: ct);

    public Task<ApiResult<SpecimenDto>> RejectSpecimenAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<SpecimenDto>(HttpMethod.Post, $"api/specimens/{id}/reject", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<List<SpecimenDto>>> GetPatientSpecimensAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<SpecimenDto>>(HttpMethod.Get, $"api/patients/{patientId}/specimens", ct: ct);

    // ---- Results ----
    public Task<ApiResult<TestResultDto>> EnterResultAsync(EnterResultRequest req, CancellationToken ct = default) =>
        SendAsync<TestResultDto>(HttpMethod.Post, "api/results", req, ct);

    public Task<ApiResult<TestResultDto>> EnterAboRhAsync(EnterAboRhRequest req, CancellationToken ct = default) =>
        SendAsync<TestResultDto>(HttpMethod.Post, "api/results/abo-rh", req, ct);

    public Task<ApiResult<TestResultDto>> VerifyResultAsync(long id, VerifyResultRequest? req = null, long? signatureId = null, CancellationToken ct = default)
    {
        var headers = signatureId is not null
            ? new Dictionary<string, string> { ["X-Esignature-Id"] = signatureId.Value.ToString() }
            : null;
        return SendAsync<TestResultDto>(HttpMethod.Post, $"api/results/{id}/verify", req ?? new VerifyResultRequest(), ct, headers);
    }

    public Task<ApiResult<TestResultDto>> CorrectResultAsync(long id, CorrectResultRequest req, CancellationToken ct = default) =>
        SendAsync<TestResultDto>(HttpMethod.Post, $"api/results/{id}/correct", req, ct);

    public Task<ApiResult<List<TestResultDto>>> GetSpecimenResultsAsync(long specimenId, CancellationToken ct = default) =>
        SendAsync<List<TestResultDto>>(HttpMethod.Get, $"api/specimens/{specimenId}/results", ct: ct);

    public Task<ApiResult<TestResultDto>> SaveTestResultAsync(SaveTestResultRequest req, long? signatureId = null, CancellationToken ct = default)
    {
        var headers = signatureId is not null
            ? new Dictionary<string, string> { ["X-Esignature-Id"] = signatureId.Value.ToString() }
            : null;
        return SendAsync<TestResultDto>(HttpMethod.Post, "api/results/save", req, ct, headers);
    }

    public Task<ApiResult<List<TestWorkItemDto>>> GetPatientTestWorklistAsync(
        long patientId,
        string status = "pending",
        string? q = null,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"status={Uri.EscapeDataString(status)}" };
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        return SendAsync<List<TestWorkItemDto>>(HttpMethod.Get, $"api/patients/{patientId}/test-worklist?{string.Join("&", query)}", ct: ct);
    }

    public Task<ApiResult<List<TestWorkItemDto>>> GetPendingTestWorklistAsync(CancellationToken ct = default) =>
        SendAsync<List<TestWorkItemDto>>(HttpMethod.Get, "api/test-worklist/pending", ct: ct);

    public Task<ApiResult<List<TestWorkItemDto>>> GetSpecimenTestWorklistAsync(
        long specimenId,
        string status = "all",
        CancellationToken ct = default) =>
        SendAsync<List<TestWorkItemDto>>(HttpMethod.Get, $"api/specimens/{specimenId}/test-worklist?status={Uri.EscapeDataString(status)}", ct: ct);

    // ---- Immunohematology ----
    public Task<ApiResult<BloodTypeDto>> GetCurrentBloodTypeAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<BloodTypeDto>(HttpMethod.Get, $"api/patients/{patientId}/blood-type", ct: ct);

    public Task<ApiResult<List<BloodTypeDto>>> GetBloodTypeHistoryAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<BloodTypeDto>>(HttpMethod.Get, $"api/patients/{patientId}/blood-type/history", ct: ct);

    public Task<ApiResult<BloodTypeDto>> RecordBloodTypeAsync(long patientId, RecordBloodTypeRequest req, CancellationToken ct = default) =>
        SendAsync<BloodTypeDto>(HttpMethod.Post, $"api/patients/{patientId}/blood-type", req, ct);

    public Task<ApiResult<List<AntibodyDto>>> GetActiveAntibodiesAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<AntibodyDto>>(HttpMethod.Get, $"api/patients/{patientId}/antibodies", ct: ct);

    public Task<ApiResult<List<AntibodyDto>>> GetAntibodyHistoryAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<AntibodyDto>>(HttpMethod.Get, $"api/patients/{patientId}/antibodies/history", ct: ct);

    public Task<ApiResult<AntibodyDto>> AddAntibodyAsync(long patientId, AddAntibodyRequest req, CancellationToken ct = default) =>
        SendAsync<AntibodyDto>(HttpMethod.Post, $"api/patients/{patientId}/antibodies", req, ct);

    public Task<ApiResult<AntibodyDto>> DeactivateAntibodyAsync(long antibodyId, string reason, CancellationToken ct = default) =>
        SendAsync<AntibodyDto>(HttpMethod.Post, $"api/antibodies/{antibodyId}/deactivate", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<List<AntigenProfileDto>>> GetAntigenProfilesAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<AntigenProfileDto>>(HttpMethod.Get, $"api/patients/{patientId}/antigen-profiles", ct: ct);

    public Task<ApiResult<AntigenProfileDto>> SaveAntigenProfileAsync(long patientId, SaveAntigenProfileRequest req, CancellationToken ct = default) =>
        SendAsync<AntigenProfileDto>(HttpMethod.Post, $"api/patients/{patientId}/antigen-profiles", req, ct);

    // ---- Inventory ----
    public Task<ApiResult<List<BloodUnitDto>>> SearchUnitsAsync(InventorySearchCriteria c, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.UnitNumber)) q.Add($"unitNumber={Uri.EscapeDataString(c.UnitNumber)}");
        if (c.Status is not null) q.Add($"status={c.Status}");
        if (c.Abo is not null) q.Add($"abo={c.Abo}");
        if (c.RhD is not null) q.Add($"rh={c.RhD}");
        if (c.ProductTypeId is not null) q.Add($"productTypeId={c.ProductTypeId}");
        if (c.LocationId is not null) q.Add($"locationId={c.LocationId}");
        if (c.ExpiringBeforeUtc is not null) q.Add($"expiringBeforeUtc={Uri.EscapeDataString(c.ExpiringBeforeUtc.Value.ToString("o"))}");
        var query = q.Count > 0 ? "?" + string.Join("&", q) : string.Empty;
        return SendAsync<List<BloodUnitDto>>(HttpMethod.Get, $"api/inventory/units{query}", ct: ct);
    }

    public Task<ApiResult<BloodUnitDto>> GetUnitAsync(long id, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Get, $"api/inventory/units/{id}", ct: ct);

    public Task<ApiResult<List<InventoryStatusHistoryDto>>> GetUnitHistoryAsync(long id, CancellationToken ct = default) =>
        SendAsync<List<InventoryStatusHistoryDto>>(HttpMethod.Get, $"api/inventory/units/{id}/history", ct: ct);

    public Task<ApiResult<List<UnitBloodAttributeDto>>> GetUnitBloodAttributesAsync(long unitId, CancellationToken ct = default) =>
        SendAsync<List<UnitBloodAttributeDto>>(HttpMethod.Get, $"api/inventory/units/{unitId}/blood-attributes", ct: ct);

    public Task<ApiResult<UnitBloodAttributeDto>> SaveUnitBloodAttributeAsync(long unitId, SaveUnitBloodAttributeRequest req, CancellationToken ct = default) =>
        SendAsync<UnitBloodAttributeDto>(HttpMethod.Post, $"api/inventory/units/{unitId}/blood-attributes", req, ct);

    public Task<ApiResult<BloodUnitDto>> ReceiveUnitAsync(ReceiveUnitRequest req, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, "api/inventory/units", req, ct);

    public Task<ApiResult<BloodUnitDto>> ReleaseUnitAsync(long id, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, $"api/inventory/units/{id}/release", ct: ct);

    public Task<ApiResult<BloodUnitDto>> HoldUnitAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, $"api/inventory/units/{id}/hold", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<BloodUnitDto>> ReleaseHoldAsync(long id, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, $"api/inventory/units/{id}/release-hold", ct: ct);

    public Task<ApiResult<BloodUnitDto>> TransferUnitAsync(long id, long toLocationId, string? reason, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, $"api/inventory/units/{id}/transfer", new TransferRequestVm(toLocationId, reason), ct);

    public Task<ApiResult<BloodUnitDto>> DiscardUnitAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, $"api/inventory/units/{id}/discard", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<ExpireDueVm>> ExpireDueAsync(CancellationToken ct = default) =>
        SendAsync<ExpireDueVm>(HttpMethod.Post, "api/inventory/expire-due", ct: ct);

    public Task<ApiResult<List<ProductRetypeWorkItemDto>>> GetPendingRetypesAsync(CancellationToken ct = default) =>
        SendAsync<List<ProductRetypeWorkItemDto>>(HttpMethod.Get, "api/inventory/retypes/pending", ct: ct);

    public Task<ApiResult<ProductRetypeDetailDto>> GetUnitRetypeAsync(long unitId, CancellationToken ct = default) =>
        SendAsync<ProductRetypeDetailDto>(HttpMethod.Get, $"api/inventory/units/{unitId}/retype", ct: ct);

    public Task<ApiResult<ProductRetypeDetailDto>> RecordUnitRetypeAsync(long unitId, RecordProductRetypeRequest req, CancellationToken ct = default) =>
        SendAsync<ProductRetypeDetailDto>(HttpMethod.Post, $"api/inventory/units/{unitId}/retype", req, ct);

    // ---- ISBT 128 ----
    public Task<ApiResult<ParseIsbtInputResponse>> ParseIsbtAsync(ParseIsbtInputRequest req, CancellationToken ct = default) =>
        SendAsync<ParseIsbtInputResponse>(HttpMethod.Post, "api/isbt/parse", req, ct);

    public Task<ApiResult<ScanSessionDto>> StartIsbtScanSessionAsync(StartScanSessionRequest req, CancellationToken ct = default) =>
        SendAsync<ScanSessionDto>(HttpMethod.Post, "api/isbt/scan-sessions", req, ct);

    public Task<ApiResult<ScanSessionDto>> AddIsbtScanAsync(AddScanRequest req, CancellationToken ct = default) =>
        SendAsync<ScanSessionDto>(HttpMethod.Post, "api/isbt/scan-sessions/scans", req, ct);

    public Task<ApiResult<BloodUnitDto>> CompleteIsbtScanSessionAsync(CompleteScanSessionRequest req, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, "api/isbt/scan-sessions/complete", req, ct);

    public Task<ApiResult<BloodUnitDto>> CreateIsbtManualAsync(ManualComponentEntryRequest req, CancellationToken ct = default) =>
        SendAsync<BloodUnitDto>(HttpMethod.Post, "api/isbt/manual-entry", req, ct);

    // ---- Compatibility ----
    public Task<ApiResult<CrossmatchDto>> RecordCrossmatchAsync(RecordCrossmatchRequest req, CancellationToken ct = default) =>
        SendAsync<CrossmatchDto>(HttpMethod.Post, "api/crossmatches", req, ct);

    public Task<ApiResult<AllocationDto>> AllocateUnitAsync(AllocateUnitRequest req, CancellationToken ct = default) =>
        SendAsync<AllocationDto>(HttpMethod.Post, "api/allocations", req, ct);

    public Task<ApiResult<AllocationDto>> ReleaseAllocationAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<AllocationDto>(HttpMethod.Post, $"api/allocations/{id}/release", new ReasonRequestVm(reason), ct);

    // ---- Issuing ----
    public Task<ApiResult<IssueDto>> IssueUnitAsync(IssueUnitRequest req, long? signatureId = null, CancellationToken ct = default)
    {
        var headers = signatureId is not null
            ? new Dictionary<string, string> { ["X-Esignature-Id"] = signatureId.Value.ToString() }
            : null;
        return SendAsync<IssueDto>(HttpMethod.Post, "api/issues", req, ct, headers);
    }

    public Task<ApiResult<IssueDto>> GetIssueAsync(long id, CancellationToken ct = default) =>
        SendAsync<IssueDto>(HttpMethod.Get, $"api/issues/{id}", ct: ct);

    public Task<ApiResult<IssueDto>> RecordWardReceiptAsync(long issueId, WardReceiptRequest req, CancellationToken ct = default) =>
        SendAsync<IssueDto>(HttpMethod.Post, $"api/issues/{issueId}/ward-receipt", req, ct);

    public Task<ApiResult<ReturnDto>> ReturnUnitAsync(long issueId, ReturnUnitRequest req, CancellationToken ct = default) =>
        SendAsync<ReturnDto>(HttpMethod.Post, $"api/issues/{issueId}/return", req, ct);

    public Task<ApiResult<TransfusionEventDto>> DocumentTransfusionAsync(long issueId, DocumentTransfusionRequest req, CancellationToken ct = default) =>
        SendAsync<TransfusionEventDto>(HttpMethod.Post, $"api/issues/{issueId}/transfusion", req, ct);

    // ---- Signatures ----
    public Task<ApiResult<SignatureCreatedVm>> RecordSignatureAsync(SignatureRequestVm req, CancellationToken ct = default) =>
        SendAsync<SignatureCreatedVm>(HttpMethod.Post, "api/signatures", req, ct);

    // ---- HL7 ----
    public async Task<ApiResult<string>> SendInboundHl7Async(string raw, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/hl7/inbound")
        {
            Content = new StringContent(raw, Encoding.UTF8, "text/plain")
        };
        try
        {
            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? ApiResult<string>.Ok(body)
                : ApiResult<string>.Fail(body, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<string>.Fail(ex.Message, 0);
        }
    }

    public Task<ApiResult<List<Hl7MessageVm>>> GetHl7MessagesAsync(CancellationToken ct = default) =>
        SendAsync<List<Hl7MessageVm>>(HttpMethod.Get, "api/hl7/messages", ct: ct);

    public Task<ApiResult<Hl7MessageDetailVm>> GetHl7MessageAsync(long id, CancellationToken ct = default) =>
        SendAsync<Hl7MessageDetailVm>(HttpMethod.Get, $"api/hl7/messages/{id}", ct: ct);

    public Task<ApiResult<Hl7ReplayVm>> ReplayHl7Async(long id, CancellationToken ct = default) =>
        SendAsync<Hl7ReplayVm>(HttpMethod.Post, $"api/hl7/messages/{id}/replay", ct: ct);

    public Task<ApiResult<List<Hl7ErrorVm>>> GetHl7ErrorsAsync(CancellationToken ct = default) =>
        SendAsync<List<Hl7ErrorVm>>(HttpMethod.Get, "api/hl7/errors", ct: ct);

    public Task<ApiResult<Hl7MessageVm>> QueueOutboundOruAsync(long resultId, CancellationToken ct = default) =>
        SendAsync<Hl7MessageVm>(HttpMethod.Post, $"api/hl7/outbound/results/{resultId}", ct: ct);

    public Task<ApiResult<Hl7MessageVm>> SendOutboundHl7Async(long id, CancellationToken ct = default) =>
        SendAsync<Hl7MessageVm>(HttpMethod.Post, $"api/hl7/messages/{id}/send", ct: ct);

    public Task<ApiResult<Hl7FlushVm>> FlushOutboundHl7Async(CancellationToken ct = default) =>
        SendAsync<Hl7FlushVm>(HttpMethod.Post, "api/hl7/outbound/flush", ct: ct);

    public Task<ApiResult<Hl7FileDropPollVm>> PollHl7FileDropAsync(CancellationToken ct = default) =>
        SendAsync<Hl7FileDropPollVm>(HttpMethod.Post, "api/hl7/file-drop/poll", ct: ct);

    // ---- Printing ----
    public Task<ApiResult<PrintJobVm>> PrintSpecimenLabelAsync(long specimenId, PrintRequestVm req, CancellationToken ct = default) =>
        SendAsync<PrintJobVm>(HttpMethod.Post, $"api/print/specimen-labels/{specimenId}", req, ct);

    public Task<ApiResult<PrintJobVm>> PrintCompatibilityTagAsync(long issueId, PrintRequestVm req, CancellationToken ct = default) =>
        SendAsync<PrintJobVm>(HttpMethod.Post, $"api/print/compatibility-tags/{issueId}", req, ct);

    public Task<ApiResult<PrintJobVm>> PrintComponentLabelAsync(long unitId, PrintRequestVm req, CancellationToken ct = default) =>
        SendAsync<PrintJobVm>(HttpMethod.Post, $"api/print/component-labels/{unitId}", req, ct);

    public Task<ApiResult<PrintJobVm>> ReprintJobAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<PrintJobVm>(HttpMethod.Post, $"api/print/jobs/{id}/reprint", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<List<PrintJobVm>>> GetPrintJobsAsync(CancellationToken ct = default) =>
        SendAsync<List<PrintJobVm>>(HttpMethod.Get, "api/print/jobs", ct: ct);

    public Task<ApiResult<PrintJobVm>> GetPrintJobAsync(long id, CancellationToken ct = default) =>
        SendAsync<PrintJobVm>(HttpMethod.Get, $"api/print/jobs/{id}", ct: ct);

    // ---- Billing ----
    public Task<ApiResult<List<BillingEventDto>>> GetBillingQueueAsync(CancellationToken ct = default) =>
        SendAsync<List<BillingEventDto>>(HttpMethod.Get, "api/billing/charges", ct: ct);

    public Task<ApiResult<BillingEventDto>> ReviewChargeAsync(long id, CancellationToken ct = default) =>
        SendAsync<BillingEventDto>(HttpMethod.Post, $"api/billing/charges/{id}/review", ct: ct);

    public Task<ApiResult<BillingEventDto>> CancelChargeAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<BillingEventDto>(HttpMethod.Post, $"api/billing/charges/{id}/cancel", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<BillingEventDto>> ExportChargeAsync(long id, CancellationToken ct = default) =>
        SendAsync<BillingEventDto>(HttpMethod.Post, $"api/billing/charges/{id}/export", ct: ct);

    // ---- Reference ----
    public Task<ApiResult<List<ProductTypeDto>>> GetProductTypesAsync(CancellationToken ct = default) =>
        SendAsync<List<ProductTypeDto>>(HttpMethod.Get, "api/reference/product-types", ct: ct);

    public Task<ApiResult<List<InventoryLocationDto>>> GetLocationsAsync(CancellationToken ct = default) =>
        SendAsync<List<InventoryLocationDto>>(HttpMethod.Get, "api/reference/locations", ct: ct);

    public Task<ApiResult<TestDefinitionForEntryDto>> GetTestDefinitionForEntryAsync(string testCode, CancellationToken ct = default) =>
        SendAsync<TestDefinitionForEntryDto>(HttpMethod.Get, $"api/reference/test-definitions/{Uri.EscapeDataString(testCode)}", ct: ct);

    public Task<ApiResult<List<OrderingLocationRefDto>>> GetOrderingLocationsAsync(CancellationToken ct = default) =>
        SendAsync<List<OrderingLocationRefDto>>(HttpMethod.Get, "api/reference/ordering-locations", ct: ct);

    public Task<ApiResult<List<OrderingProviderRefDto>>> GetOrderingProvidersAsync(CancellationToken ct = default) =>
        SendAsync<List<OrderingProviderRefDto>>(HttpMethod.Get, "api/reference/ordering-providers", ct: ct);

    public Task<ApiResult<List<DirectoryUserDto>>> GetDirectoryUsersAsync(CancellationToken ct = default) =>
        SendAsync<List<DirectoryUserDto>>(HttpMethod.Get, "api/reference/users", ct: ct);

    public Task<ApiResult<List<TestDefinitionListItemDto>>> GetTestDefinitionsListAsync(CancellationToken ct = default) =>
        SendAsync<List<TestDefinitionListItemDto>>(HttpMethod.Get, "api/reference/test-definitions", ct: ct);

    // ---- Audit ----
    public Task<ApiResult<AuditPageVm>> GetAuditEventsAsync(
        string? entityType = null,
        long? entityId = null,
        int skip = 0,
        int take = 200,
        CancellationToken ct = default)
    {
        var q = new List<string>
        {
            $"skip={skip}",
            $"take={take}"
        };
        if (!string.IsNullOrWhiteSpace(entityType)) q.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (entityId is not null) q.Add($"entityId={entityId}");
        return SendAsync<AuditPageVm>(HttpMethod.Get, $"api/audit-events?{string.Join("&", q)}", ct: ct);
    }

    public Task<ApiResult<List<SpecialRequirementDto>>> GetSpecialRequirementsAsync(long patientId, CancellationToken ct = default) =>
        SendAsync<List<SpecialRequirementDto>>(HttpMethod.Get, $"api/patients/{patientId}/special-requirements", ct: ct);

    public Task<ApiResult<SpecialRequirementDto>> AddSpecialRequirementAsync(long patientId, AddSpecialRequirementRequest req, CancellationToken ct = default) =>
        SendAsync<SpecialRequirementDto>(HttpMethod.Post, $"api/patients/{patientId}/special-requirements", req, ct);

    public Task<ApiResult<SpecialRequirementDto>> DeactivateSpecialRequirementAsync(long id, string reason, CancellationToken ct = default) =>
        SendAsync<SpecialRequirementDto>(HttpMethod.Post, $"api/special-requirements/{id}/deactivate", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<LookbackReportDto>> LookbackAsync(string din, CancellationToken ct = default) =>
        SendAsync<LookbackReportDto>(HttpMethod.Get, $"api/lookback/{Uri.EscapeDataString(din)}", ct: ct);

    public Task<ApiResult<RecipientTraceReportDto>> LookbackByRecipientAsync(
        string? mrn, long? patientId = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(mrn)) q.Add($"mrn={Uri.EscapeDataString(mrn)}");
        if (patientId is > 0) q.Add($"patientId={patientId}");
        return SendAsync<RecipientTraceReportDto>(
            HttpMethod.Get, $"api/lookback/recipient?{string.Join("&", q)}", ct: ct);
    }

    public Task<ApiResult<LookbackReportDto>> RecallByDinAsync(string din, string reason, CancellationToken ct = default) =>
        SendAsync<LookbackReportDto>(HttpMethod.Post, $"api/lookback/{Uri.EscapeDataString(din)}/recall", new ReasonRequestVm(reason), ct);

    public Task<ApiResult<LookbackNotificationDto>> RecordLookbackAttemptAsync(long id, RecordLookbackAttemptRequest req, CancellationToken ct = default) =>
        SendAsync<LookbackNotificationDto>(HttpMethod.Post, $"api/lookback/notifications/{id}", req, ct);

    public Task<ApiResult<List<ReactionInvestigationDto>>> GetReactionInvestigationsAsync(CancellationToken ct = default) =>
        SendAsync<List<ReactionInvestigationDto>>(HttpMethod.Get, "api/reaction-investigations", ct: ct);

    public Task<ApiResult<ReactionInvestigationDto>> UpdateReactionInvestigationAsync(long id, UpdateReactionInvestigationRequest req, CancellationToken ct = default) =>
        SendAsync<ReactionInvestigationDto>(HttpMethod.Put, $"api/reaction-investigations/{id}", req, ct);

    public Task<ApiResult<ReactionInvestigationDto>> RecordCberNotificationAsync(long id, CancellationToken ct = default) =>
        SendAsync<ReactionInvestigationDto>(HttpMethod.Post, $"api/reaction-investigations/{id}/cber-notified", ct: ct);

    public Task<ApiResult<ReactionInvestigationDto>> RecordWrittenReportAsync(long id, CancellationToken ct = default) =>
        SendAsync<ReactionInvestigationDto>(HttpMethod.Post, $"api/reaction-investigations/{id}/written-report", ct: ct);

    public Task<ApiResult<List<DeviationDto>>> GetDeviationsAsync(CancellationToken ct = default) =>
        SendAsync<List<DeviationDto>>(HttpMethod.Get, "api/deviations", ct: ct);

    public Task<ApiResult<DeviationDto>> CreateDeviationAsync(CreateDeviationRequest req, CancellationToken ct = default) =>
        SendAsync<DeviationDto>(HttpMethod.Post, "api/deviations", req, ct);

    public Task<ApiResult<DeviationDto>> UpdateDeviationStatusAsync(long id, DeviationStatus status, string? correctiveAction = null, CancellationToken ct = default) =>
        SendAsync<DeviationDto>(HttpMethod.Post, $"api/deviations/{id}/status", new { status, correctiveAction }, ct);

    // ---- Admin: Tests ----
    public Task<ApiResult<List<TestDefinitionDto>>> GetAdminTestsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<TestDefinitionDto>>(HttpMethod.Get, $"api/admin/tests?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<TestDefinitionDto>> GetAdminTestAsync(long id, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Get, $"api/admin/tests/{id}", ct: ct);

    public Task<ApiResult<TestDefinitionDto>> CreateAdminTestAsync(SaveTestDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Post, "api/admin/tests", req, ct);

    public Task<ApiResult<TestDefinitionDto>> UpdateAdminTestAsync(long id, SaveTestDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Put, $"api/admin/tests/{id}", req, ct);

    public Task<ApiResult<TestDefinitionDto>> ActivateAdminTestAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Post, $"api/admin/tests/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<TestDefinitionDto>> DeactivateAdminTestAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Post, $"api/admin/tests/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<TestDefinitionDto>> CloneAdminTestAsync(long id, string newCode, CancellationToken ct = default) =>
        SendAsync<TestDefinitionDto>(HttpMethod.Post, $"api/admin/tests/{id}/clone", new CloneRequest(newCode), ct);

    // ---- Admin: Blood attributes ----
    public Task<ApiResult<List<BloodAttributeDefinitionDto>>> GetAdminBloodAttributesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<BloodAttributeDefinitionDto>>(HttpMethod.Get, $"api/admin/blood-attributes?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<BloodAttributeDefinitionDto>> GetAdminBloodAttributeAsync(long id, CancellationToken ct = default) =>
        SendAsync<BloodAttributeDefinitionDto>(HttpMethod.Get, $"api/admin/blood-attributes/{id}", ct: ct);

    public Task<ApiResult<BloodAttributeDefinitionDto>> CreateAdminBloodAttributeAsync(SaveBloodAttributeDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<BloodAttributeDefinitionDto>(HttpMethod.Post, "api/admin/blood-attributes", req, ct);

    public Task<ApiResult<BloodAttributeDefinitionDto>> UpdateAdminBloodAttributeAsync(long id, SaveBloodAttributeDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<BloodAttributeDefinitionDto>(HttpMethod.Put, $"api/admin/blood-attributes/{id}", req, ct);

    public Task<ApiResult<BloodAttributeDefinitionDto>> ActivateAdminBloodAttributeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<BloodAttributeDefinitionDto>(HttpMethod.Post, $"api/admin/blood-attributes/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<BloodAttributeDefinitionDto>> DeactivateAdminBloodAttributeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<BloodAttributeDefinitionDto>(HttpMethod.Post, $"api/admin/blood-attributes/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<BloodAttributeListItemDto>>> GetReferenceBloodAttributesAsync(CancellationToken ct = default) =>
        SendAsync<List<BloodAttributeListItemDto>>(HttpMethod.Get, "api/reference/blood-attributes", ct: ct);

    // ---- Admin: Specimen types ----
    public Task<ApiResult<List<SpecimenTypeDefinitionDto>>> GetAdminSpecimenTypesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<SpecimenTypeDefinitionDto>>(HttpMethod.Get, $"api/admin/specimen-types?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<SpecimenTypeDefinitionDto>> GetAdminSpecimenTypeAsync(long id, CancellationToken ct = default) =>
        SendAsync<SpecimenTypeDefinitionDto>(HttpMethod.Get, $"api/admin/specimen-types/{id}", ct: ct);

    public Task<ApiResult<SpecimenTypeDefinitionDto>> CreateAdminSpecimenTypeAsync(SaveSpecimenTypeDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<SpecimenTypeDefinitionDto>(HttpMethod.Post, "api/admin/specimen-types", req, ct);

    public Task<ApiResult<SpecimenTypeDefinitionDto>> UpdateAdminSpecimenTypeAsync(long id, SaveSpecimenTypeDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<SpecimenTypeDefinitionDto>(HttpMethod.Put, $"api/admin/specimen-types/{id}", req, ct);

    public Task<ApiResult<SpecimenTypeDefinitionDto>> ActivateAdminSpecimenTypeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<SpecimenTypeDefinitionDto>(HttpMethod.Post, $"api/admin/specimen-types/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<SpecimenTypeDefinitionDto>> DeactivateAdminSpecimenTypeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<SpecimenTypeDefinitionDto>(HttpMethod.Post, $"api/admin/specimen-types/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<SpecimenTypeListItemDto>>> GetReferenceSpecimenTypesAsync(CancellationToken ct = default) =>
        SendAsync<List<SpecimenTypeListItemDto>>(HttpMethod.Get, "api/reference/specimen-types", ct: ct);

    // ---- Admin: Subtests ----
    public Task<ApiResult<List<PhaseDefinitionDto>>> GetAdminPhasesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<PhaseDefinitionDto>>(HttpMethod.Get, $"api/admin/phases?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<PhaseDefinitionDto>> GetAdminPhaseAsync(long id, CancellationToken ct = default) =>
        SendAsync<PhaseDefinitionDto>(HttpMethod.Get, $"api/admin/phases/{id}", ct: ct);

    public Task<ApiResult<PhaseDefinitionDto>> CreateAdminPhaseAsync(SavePhaseDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<PhaseDefinitionDto>(HttpMethod.Post, "api/admin/phases", req, ct);

    public Task<ApiResult<PhaseDefinitionDto>> UpdateAdminPhaseAsync(long id, SavePhaseDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<PhaseDefinitionDto>(HttpMethod.Put, $"api/admin/phases/{id}", req, ct);

    public Task<ApiResult<PhaseDefinitionDto>> ActivateAdminPhaseAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<PhaseDefinitionDto>(HttpMethod.Post, $"api/admin/phases/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<PhaseDefinitionDto>> DeactivateAdminPhaseAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<PhaseDefinitionDto>(HttpMethod.Post, $"api/admin/phases/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<PhaseListItemDto>>> GetReferencePhasesAsync(CancellationToken ct = default) =>
        SendAsync<List<PhaseListItemDto>>(HttpMethod.Get, "api/reference/phases", ct: ct);

    public Task<ApiResult<List<SubtestDefinitionDto>>> GetAdminSubtestsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<SubtestDefinitionDto>>(HttpMethod.Get, $"api/admin/subtests?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<SubtestDefinitionDto>> GetAdminSubtestAsync(long id, CancellationToken ct = default) =>
        SendAsync<SubtestDefinitionDto>(HttpMethod.Get, $"api/admin/subtests/{id}", ct: ct);

    public Task<ApiResult<SubtestDefinitionDto>> CreateAdminSubtestAsync(SaveSubtestDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<SubtestDefinitionDto>(HttpMethod.Post, "api/admin/subtests", req, ct);

    public Task<ApiResult<SubtestDefinitionDto>> UpdateAdminSubtestAsync(long id, SaveSubtestDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<SubtestDefinitionDto>(HttpMethod.Put, $"api/admin/subtests/{id}", req, ct);

    public Task<ApiResult<SubtestDefinitionDto>> ActivateAdminSubtestAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<SubtestDefinitionDto>(HttpMethod.Post, $"api/admin/subtests/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<SubtestDefinitionDto>> DeactivateAdminSubtestAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<SubtestDefinitionDto>(HttpMethod.Post, $"api/admin/subtests/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Admin: Test groupers ----
    public Task<ApiResult<List<TestGrouperDto>>> GetAdminTestGroupersAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<TestGrouperDto>>(HttpMethod.Get, $"api/admin/test-groupers?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<TestGrouperDto>> GetAdminTestGrouperAsync(long id, CancellationToken ct = default) =>
        SendAsync<TestGrouperDto>(HttpMethod.Get, $"api/admin/test-groupers/{id}", ct: ct);

    public Task<ApiResult<TestGrouperDto>> CreateAdminTestGrouperAsync(SaveTestGrouperRequest req, CancellationToken ct = default) =>
        SendAsync<TestGrouperDto>(HttpMethod.Post, "api/admin/test-groupers", req, ct);

    public Task<ApiResult<TestGrouperDto>> UpdateAdminTestGrouperAsync(long id, SaveTestGrouperRequest req, CancellationToken ct = default) =>
        SendAsync<TestGrouperDto>(HttpMethod.Put, $"api/admin/test-groupers/{id}", req, ct);

    public Task<ApiResult<TestGrouperDto>> ActivateAdminTestGrouperAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<TestGrouperDto>(HttpMethod.Post, $"api/admin/test-groupers/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<TestGrouperDto>> DeactivateAdminTestGrouperAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<TestGrouperDto>(HttpMethod.Post, $"api/admin/test-groupers/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Admin: Reflex rules ----
    public Task<ApiResult<List<ReflexRuleDto>>> GetAdminReflexRulesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ReflexRuleDto>>(HttpMethod.Get, $"api/admin/reflex-rules?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ReflexRuleDto>> GetAdminReflexRuleAsync(long id, CancellationToken ct = default) =>
        SendAsync<ReflexRuleDto>(HttpMethod.Get, $"api/admin/reflex-rules/{id}", ct: ct);

    public Task<ApiResult<ReflexRuleDto>> CreateAdminReflexRuleAsync(SaveReflexRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ReflexRuleDto>(HttpMethod.Post, "api/admin/reflex-rules", req, ct);

    public Task<ApiResult<ReflexRuleDto>> UpdateAdminReflexRuleAsync(long id, SaveReflexRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ReflexRuleDto>(HttpMethod.Put, $"api/admin/reflex-rules/{id}", req, ct);

    public Task<ApiResult<ReflexRuleDto>> ActivateAdminReflexRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ReflexRuleDto>(HttpMethod.Post, $"api/admin/reflex-rules/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<ReflexRuleDto>> DeactivateAdminReflexRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ReflexRuleDto>(HttpMethod.Post, $"api/admin/reflex-rules/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Admin: Order and test rules ----
    public Task<ApiResult<List<RuleDefinitionDto>>> GetAdminRulesAsync(
        bool includeInactive = true,
        RuleLevel? level = null,
        CancellationToken ct = default)
    {
        var query = $"includeInactive={includeInactive.ToString().ToLowerInvariant()}";
        if (level is not null)
        {
            query += $"&level={level}";
        }

        return SendAsync<List<RuleDefinitionDto>>(HttpMethod.Get, $"api/admin/rules?{query}", ct: ct);
    }

    public Task<ApiResult<RuleDefinitionDto>> GetAdminRuleAsync(long id, CancellationToken ct = default) =>
        SendAsync<RuleDefinitionDto>(HttpMethod.Get, $"api/admin/rules/{id}", ct: ct);

    public Task<ApiResult<RuleVocabularyDto>> GetAdminRuleVocabularyAsync(RuleLevel level, CancellationToken ct = default) =>
        SendAsync<RuleVocabularyDto>(HttpMethod.Get, $"api/admin/rules/vocabulary?level={level}", ct: ct);

    public Task<ApiResult<RuleHelpDto>> GetAdminRuleHelpAsync(CancellationToken ct = default) =>
        SendAsync<RuleHelpDto>(HttpMethod.Get, "api/admin/rules/help", ct: ct);

    public Task<ApiResult<RuleValidationDto>> ValidateAdminRuleAsync(ValidateRuleRequest req, CancellationToken ct = default) =>
        SendAsync<RuleValidationDto>(HttpMethod.Post, "api/admin/rules/validate", req, ct);

    public Task<ApiResult<RuleDefinitionDto>> CreateAdminRuleAsync(SaveRuleDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<RuleDefinitionDto>(HttpMethod.Post, "api/admin/rules", req, ct);

    public Task<ApiResult<RuleDefinitionDto>> UpdateAdminRuleAsync(long id, SaveRuleDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<RuleDefinitionDto>(HttpMethod.Put, $"api/admin/rules/{id}", req, ct);

    public Task<ApiResult<RuleDefinitionDto>> ActivateAdminRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<RuleDefinitionDto>(HttpMethod.Post, $"api/admin/rules/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<RuleDefinitionDto>> DeactivateAdminRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<RuleDefinitionDto>(HttpMethod.Post, $"api/admin/rules/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<SubtestListItemDto>>> GetReferenceSubtestsAsync(CancellationToken ct = default) =>
        SendAsync<List<SubtestListItemDto>>(HttpMethod.Get, "api/reference/subtests", ct: ct);

    public Task<ApiResult<List<TestGrouperListItemDto>>> GetReferenceTestGroupersAsync(CancellationToken ct = default) =>
        SendAsync<List<TestGrouperListItemDto>>(HttpMethod.Get, "api/reference/test-groupers", ct: ct);

    // ---- Admin: Providers ----
    public Task<ApiResult<List<ExceptionDefinitionDto>>> GetAdminExceptionsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ExceptionDefinitionDto>>(HttpMethod.Get, $"api/admin/exceptions?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ExceptionDefinitionDto>> GetAdminExceptionByCodeAsync(string ruleCode, CancellationToken ct = default) =>
        SendAsync<ExceptionDefinitionDto>(HttpMethod.Get, $"api/admin/exceptions/by-code/{Uri.EscapeDataString(ruleCode)}", ct: ct);

    public Task<ApiResult<ExceptionDefinitionDto>> CreateAdminExceptionAsync(SaveExceptionDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<ExceptionDefinitionDto>(HttpMethod.Post, "api/admin/exceptions", req, ct);

    public Task<ApiResult<ExceptionDefinitionDto>> UpdateAdminExceptionAsync(long id, SaveExceptionDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<ExceptionDefinitionDto>(HttpMethod.Put, $"api/admin/exceptions/{id}", req, ct);

    public Task<ApiResult<ExceptionDefinitionDto>> SetAdminExceptionActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<ExceptionDefinitionDto>(HttpMethod.Post, $"api/admin/exceptions/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    public Task<ApiResult<List<OrderingProviderDto>>> GetAdminProvidersAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<OrderingProviderDto>>(HttpMethod.Get, $"api/admin/providers?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<OrderingProviderDto>> CreateAdminProviderAsync(SaveOrderingProviderRequest req, CancellationToken ct = default) =>
        SendAsync<OrderingProviderDto>(HttpMethod.Post, "api/admin/providers", req, ct);

    public Task<ApiResult<OrderingProviderDto>> UpdateAdminProviderAsync(long id, SaveOrderingProviderRequest req, CancellationToken ct = default) =>
        SendAsync<OrderingProviderDto>(HttpMethod.Put, $"api/admin/providers/{id}", req, ct);

    public Task<ApiResult<OrderingProviderDto>> SetAdminProviderActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<OrderingProviderDto>(HttpMethod.Post, $"api/admin/providers/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Ordering locations ----
    public Task<ApiResult<List<OrderingLocationDto>>> GetAdminLocationsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<OrderingLocationDto>>(HttpMethod.Get, $"api/admin/locations?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<OrderingLocationDto>> CreateAdminLocationAsync(SaveOrderingLocationRequest req, CancellationToken ct = default) =>
        SendAsync<OrderingLocationDto>(HttpMethod.Post, "api/admin/locations", req, ct);

    public Task<ApiResult<OrderingLocationDto>> UpdateAdminLocationAsync(long id, SaveOrderingLocationRequest req, CancellationToken ct = default) =>
        SendAsync<OrderingLocationDto>(HttpMethod.Put, $"api/admin/locations/{id}", req, ct);

    public Task<ApiResult<OrderingLocationDto>> SetAdminLocationActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<OrderingLocationDto>(HttpMethod.Post, $"api/admin/locations/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Test/service billing ----
    public Task<ApiResult<List<TestServiceBillingDto>>> GetAdminTestServiceBillingsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<TestServiceBillingDto>>(HttpMethod.Get, $"api/admin/test-service-billings?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<TestServiceBillingDto>> CreateAdminTestServiceBillingAsync(SaveTestServiceBillingRequest req, CancellationToken ct = default) =>
        SendAsync<TestServiceBillingDto>(HttpMethod.Post, "api/admin/test-service-billings", req, ct);

    public Task<ApiResult<TestServiceBillingDto>> UpdateAdminTestServiceBillingAsync(long id, SaveTestServiceBillingRequest req, CancellationToken ct = default) =>
        SendAsync<TestServiceBillingDto>(HttpMethod.Put, $"api/admin/test-service-billings/{id}", req, ct);

    public Task<ApiResult<TestServiceBillingDto>> SetAdminTestServiceBillingActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<TestServiceBillingDto>(HttpMethod.Post, $"api/admin/test-service-billings/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Product billing ----
    public Task<ApiResult<List<ProductBillingDto>>> GetAdminProductBillingsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ProductBillingDto>>(HttpMethod.Get, $"api/admin/product-billings?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ProductBillingDto>> CreateAdminProductBillingAsync(SaveProductBillingRequest req, CancellationToken ct = default) =>
        SendAsync<ProductBillingDto>(HttpMethod.Post, "api/admin/product-billings", req, ct);

    public Task<ApiResult<ProductBillingDto>> UpdateAdminProductBillingAsync(long id, SaveProductBillingRequest req, CancellationToken ct = default) =>
        SendAsync<ProductBillingDto>(HttpMethod.Put, $"api/admin/product-billings/{id}", req, ct);

    public Task<ApiResult<ProductBillingDto>> SetAdminProductBillingActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<ProductBillingDto>(HttpMethod.Post, $"api/admin/product-billings/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Charge codes ----
    public Task<ApiResult<List<ChargeCodeDto>>> GetAdminChargeCodesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ChargeCodeDto>>(HttpMethod.Get, $"api/admin/charge-codes?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ChargeCodeDto>> CreateAdminChargeCodeAsync(SaveChargeCodeRequest req, CancellationToken ct = default) =>
        SendAsync<ChargeCodeDto>(HttpMethod.Post, "api/admin/charge-codes", req, ct);

    public Task<ApiResult<ChargeCodeDto>> UpdateAdminChargeCodeAsync(long id, SaveChargeCodeRequest req, CancellationToken ct = default) =>
        SendAsync<ChargeCodeDto>(HttpMethod.Put, $"api/admin/charge-codes/{id}", req, ct);

    public Task<ApiResult<ChargeCodeDto>> SetAdminChargeCodeActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<ChargeCodeDto>(HttpMethod.Post, $"api/admin/charge-codes/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Charge rules ----
    public Task<ApiResult<List<ChargeRuleDto>>> GetAdminChargeRulesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ChargeRuleDto>>(HttpMethod.Get, $"api/admin/charge-rules?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ChargeRuleDto>> CreateAdminChargeRuleAsync(SaveChargeRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ChargeRuleDto>(HttpMethod.Post, "api/admin/charge-rules", req, ct);

    public Task<ApiResult<ChargeRuleDto>> UpdateAdminChargeRuleAsync(long id, SaveChargeRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ChargeRuleDto>(HttpMethod.Put, $"api/admin/charge-rules/{id}", req, ct);

    public Task<ApiResult<ChargeRuleDto>> SetAdminChargeRuleActiveAsync(long id, bool active, CancellationToken ct = default) =>
        SendAsync<ChargeRuleDto>(HttpMethod.Post, $"api/admin/charge-rules/{id}/{(active ? "activate" : "deactivate")}", ct: ct);

    // ---- Admin: Products ----
    public Task<ApiResult<List<ProductAttributeDto>>> GetProductAttributesAsync(CancellationToken ct = default) =>
        SendAsync<List<ProductAttributeDto>>(HttpMethod.Get, "api/admin/products/attributes", ct: ct);

    public Task<ApiResult<List<ProductDefinitionDto>>> GetAdminProductsAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ProductDefinitionDto>>(HttpMethod.Get, $"api/admin/products?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ProductDefinitionDto>> GetAdminProductAsync(long id, CancellationToken ct = default) =>
        SendAsync<ProductDefinitionDto>(HttpMethod.Get, $"api/admin/products/{id}", ct: ct);

    public Task<ApiResult<ProductDefinitionDto>> CreateAdminProductAsync(SaveProductDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<ProductDefinitionDto>(HttpMethod.Post, "api/admin/products", req, ct);

    public Task<ApiResult<ProductDefinitionDto>> UpdateAdminProductAsync(long id, SaveProductDefinitionRequest req, CancellationToken ct = default) =>
        SendAsync<ProductDefinitionDto>(HttpMethod.Put, $"api/admin/products/{id}", req, ct);

    public Task<ApiResult<ProductDefinitionDto>> ActivateAdminProductAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ProductDefinitionDto>(HttpMethod.Post, $"api/admin/products/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<ProductDefinitionDto>> DeactivateAdminProductAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ProductDefinitionDto>(HttpMethod.Post, $"api/admin/products/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Inventory: Modifications ----
    public Task<ApiResult<List<EligibleModificationDto>>> GetEligibleModificationsAsync(long unitId, CancellationToken ct = default) =>
        SendAsync<List<EligibleModificationDto>>(HttpMethod.Get, $"api/inventory/units/{unitId}/eligible-modifications", ct: ct);

    public Task<ApiResult<List<UnitModificationDto>>> GetUnitModificationHistoryAsync(long unitId, CancellationToken ct = default) =>
        SendAsync<List<UnitModificationDto>>(HttpMethod.Get, $"api/inventory/units/{unitId}/modifications", ct: ct);

    public Task<ApiResult<ModificationResultVm>> DivideUnitAsync(long unitId, PerformDivideRequest req, CancellationToken ct = default) =>
        SendAsync<ModificationResultVm>(HttpMethod.Post, $"api/inventory/units/{unitId}/modifications/divide", req, ct);

    public Task<ApiResult<ModificationResultVm>> PoolUnitsAsync(PerformPoolRequest req, CancellationToken ct = default) =>
        SendAsync<ModificationResultVm>(HttpMethod.Post, "api/inventory/modifications/pool", req, ct);

    public Task<ApiResult<ModificationResultVm>> ApplyModificationAsync(long unitId, PerformSingleModificationRequest req, CancellationToken ct = default) =>
        SendAsync<ModificationResultVm>(HttpMethod.Post, $"api/inventory/units/{unitId}/modifications/apply", req, ct);

    // ---- Admin: Modification rules ----
    public Task<ApiResult<List<ModificationRuleDto>>> GetAdminModificationRulesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ModificationRuleDto>>(HttpMethod.Get, $"api/admin/modification-rules?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ModificationRuleDto>> GetAdminModificationRuleAsync(long id, CancellationToken ct = default) =>
        SendAsync<ModificationRuleDto>(HttpMethod.Get, $"api/admin/modification-rules/{id}", ct: ct);

    public Task<ApiResult<ModificationRuleDto>> CreateAdminModificationRuleAsync(SaveModificationRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ModificationRuleDto>(HttpMethod.Post, "api/admin/modification-rules", req, ct);

    public Task<ApiResult<ModificationRuleDto>> UpdateAdminModificationRuleAsync(long id, SaveModificationRuleRequest req, CancellationToken ct = default) =>
        SendAsync<ModificationRuleDto>(HttpMethod.Put, $"api/admin/modification-rules/{id}", req, ct);

    public Task<ApiResult<ModificationRuleDto>> ActivateAdminModificationRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ModificationRuleDto>(HttpMethod.Post, $"api/admin/modification-rules/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<ModificationRuleDto>> DeactivateAdminModificationRuleAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ModificationRuleDto>(HttpMethod.Post, $"api/admin/modification-rules/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Admin: Expiration modification codes ----
    public Task<ApiResult<List<ExpirationModificationCodeDto>>> GetAdminExpirationCodesAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<ExpirationModificationCodeDto>>(HttpMethod.Get, $"api/admin/expiration-codes?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<ExpirationModificationCodeDto>> GetAdminExpirationCodeAsync(long id, CancellationToken ct = default) =>
        SendAsync<ExpirationModificationCodeDto>(HttpMethod.Get, $"api/admin/expiration-codes/{id}", ct: ct);

    public Task<ApiResult<ExpirationModificationCodeDto>> CreateAdminExpirationCodeAsync(SaveExpirationModificationCodeRequest req, CancellationToken ct = default) =>
        SendAsync<ExpirationModificationCodeDto>(HttpMethod.Post, "api/admin/expiration-codes", req, ct);

    public Task<ApiResult<ExpirationModificationCodeDto>> UpdateAdminExpirationCodeAsync(long id, SaveExpirationModificationCodeRequest req, CancellationToken ct = default) =>
        SendAsync<ExpirationModificationCodeDto>(HttpMethod.Put, $"api/admin/expiration-codes/{id}", req, ct);

    public Task<ApiResult<ExpirationModificationCodeDto>> ActivateAdminExpirationCodeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ExpirationModificationCodeDto>(HttpMethod.Post, $"api/admin/expiration-codes/{id}/activate", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<ExpirationModificationCodeDto>> DeactivateAdminExpirationCodeAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<ExpirationModificationCodeDto>(HttpMethod.Post, $"api/admin/expiration-codes/{id}/deactivate", new ReasonOnlyRequest(reason), ct);

    // ---- Admin: ISBT product description codes ----
    public Task<ApiResult<List<IsbtProductCodeDto>>> GetAdminIsbtProductCodesAsync(CancellationToken ct = default) =>
        SendAsync<List<IsbtProductCodeDto>>(HttpMethod.Get, "api/admin/isbt-product-codes", ct: ct);

    // ---- Admin: HL7 endpoints / interface setup ----
    public Task<ApiResult<List<Hl7EndpointDto>>> GetAdminHl7EndpointsAsync(CancellationToken ct = default) =>
        SendAsync<List<Hl7EndpointDto>>(HttpMethod.Get, "api/admin/hl7/endpoints", ct: ct);

    public Task<ApiResult<Hl7EndpointDto>> GetAdminHl7EndpointAsync(long id, CancellationToken ct = default) =>
        SendAsync<Hl7EndpointDto>(HttpMethod.Get, $"api/admin/hl7/endpoints/{id}", ct: ct);

    public Task<ApiResult<Hl7EndpointDto>> CreateAdminHl7EndpointAsync(SaveHl7EndpointRequest req, CancellationToken ct = default) =>
        SendAsync<Hl7EndpointDto>(HttpMethod.Post, "api/admin/hl7/endpoints", req, ct);

    public Task<ApiResult<Hl7EndpointDto>> UpdateAdminHl7EndpointAsync(long id, SaveHl7EndpointRequest req, CancellationToken ct = default) =>
        SendAsync<Hl7EndpointDto>(HttpMethod.Put, $"api/admin/hl7/endpoints/{id}", req, ct);

    public Task<ApiResult<Hl7EndpointDto>> SetHl7EndpointEnabledAsync(long id, bool enabled, string? reason, CancellationToken ct = default) =>
        SendAsync<Hl7EndpointDto>(HttpMethod.Post, $"api/admin/hl7/endpoints/{id}/{(enabled ? "enable" : "disable")}", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<InterfaceDataItemDto>>> GetAdminHl7DataItemsAsync(InterfaceType type, Hl7Direction direction, CancellationToken ct = default) =>
        SendAsync<List<InterfaceDataItemDto>>(HttpMethod.Get, $"api/admin/hl7/data-items?interfaceType={type}&direction={direction}", ct: ct);

    public Task<ApiResult<List<InterfaceVendorDto>>> GetAdminHl7VendorsAsync(InterfaceType? type = null, CancellationToken ct = default)
    {
        var qs = type is null ? "" : $"?interfaceType={type}";
        return SendAsync<List<InterfaceVendorDto>>(HttpMethod.Get, $"api/admin/hl7/vendors{qs}", ct: ct);
    }

    public Task<ApiResult<InterfaceVendorPresetDto>> GetAdminHl7VendorPresetAsync(
        string code, InterfaceType type, Hl7Direction direction, CancellationToken ct = default) =>
        SendAsync<InterfaceVendorPresetDto>(
            HttpMethod.Get,
            $"api/admin/hl7/vendors/{Uri.EscapeDataString(code)}/preset?interfaceType={type}&direction={direction}",
            ct: ct);

    public Task<ApiResult<List<InterfaceDataItemDto>>> GetAdminHl7AllDataItemsAsync(CancellationToken ct = default) =>
        SendAsync<List<InterfaceDataItemDto>>(HttpMethod.Get, "api/admin/hl7/data-items/all", ct: ct);

    public Task<ApiResult<InterfaceTranslationTableDto>> GetAdminHl7TranslationsAsync(string dataItemKey, CancellationToken ct = default) =>
        SendAsync<InterfaceTranslationTableDto>(
            HttpMethod.Get,
            $"api/admin/hl7/translations?dataItemKey={Uri.EscapeDataString(dataItemKey)}",
            ct: ct);

    public Task<ApiResult<InterfaceTranslationTableDto>> SaveAdminHl7TranslationsAsync(
        string dataItemKey, SaveInterfaceTranslationsRequest req, CancellationToken ct = default) =>
        SendAsync<InterfaceTranslationTableDto>(
            HttpMethod.Put,
            $"api/admin/hl7/translations/{Uri.EscapeDataString(dataItemKey)}",
            req,
            ct);

    // ---- Admin: Users & roles ----
    public Task<ApiResult<List<AdminUserDto>>> GetAdminUsersAsync(bool includeInactive = true, CancellationToken ct = default) =>
        SendAsync<List<AdminUserDto>>(HttpMethod.Get, $"api/admin/users?includeInactive={includeInactive.ToString().ToLowerInvariant()}", ct: ct);

    public Task<ApiResult<AdminUserDto>> GetAdminUserAsync(long id, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Get, $"api/admin/users/{id}", ct: ct);

    public Task<ApiResult<AdminUserDto>> CreateAdminUserAsync(SaveUserRequest req, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, "api/admin/users", req, ct);

    public Task<ApiResult<AdminUserDto>> UpdateAdminUserAsync(long id, SaveUserRequest req, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Put, $"api/admin/users/{id}", req, ct);

    public Task<ApiResult<AdminUserDto>> AssignUserRolesAsync(long id, AssignRolesRequest req, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/roles", req, ct);

    public Task<ApiResult<AdminUserDto>> SetUserActiveAsync(long id, bool active, string? reason, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/active", new SetActiveRequest(active, reason), ct);

    public Task<ApiResult<AdminUserDto>> SetUserLockedAsync(long id, bool locked, string? reason, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/lock", new SetActiveRequest(locked, reason), ct);

    public Task<ApiResult<AdminUserDto>> ResetUserPasswordAsync(long id, string? reason, CancellationToken ct = default) =>
        SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/reset-password", new ReasonOnlyRequest(reason), ct);

    public Task<ApiResult<List<AdminRoleDto>>> GetAdminRolesAsync(CancellationToken ct = default) =>
        SendAsync<List<AdminRoleDto>>(HttpMethod.Get, "api/admin/roles", ct: ct);

    public Task<ApiResult<AdminRoleDto>> GetAdminRoleAsync(long id, CancellationToken ct = default) =>
        SendAsync<AdminRoleDto>(HttpMethod.Get, $"api/admin/roles/{id}", ct: ct);

    public Task<ApiResult<AdminRoleDto>> CreateAdminRoleAsync(SaveRoleRequest req, CancellationToken ct = default) =>
        SendAsync<AdminRoleDto>(HttpMethod.Post, "api/admin/roles", req, ct);

    public Task<ApiResult<AdminRoleDto>> UpdateAdminRoleAsync(long id, SaveRoleRequest req, CancellationToken ct = default) =>
        SendAsync<AdminRoleDto>(HttpMethod.Put, $"api/admin/roles/{id}", req, ct);

    public Task<ApiResult<List<string>>> GetPermissionCodesAsync(CancellationToken ct = default) =>
        SendAsync<List<string>>(HttpMethod.Get, "api/admin/permissions", ct: ct);

    // ---- Admin: Change history ----
    public Task<ApiResult<List<ConfigHistoryDto>>> GetConfigHistoryAsync(string? entityType = null, long? entityId = null, int max = 100, CancellationToken ct = default)
    {
        var q = new List<string> { $"max={max}" };
        if (!string.IsNullOrWhiteSpace(entityType)) q.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (entityId is not null) q.Add($"entityId={entityId}");
        return SendAsync<List<ConfigHistoryDto>>(HttpMethod.Get, $"api/admin/history?{string.Join("&", q)}", ct: ct);
    }

    // ---- Core ----
    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body = null,
        CancellationToken ct = default,
        IReadOnlyDictionary<string, string>? extraHeaders = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        if (extraHeaders is not null)
        {
            foreach (var (k, v) in extraHeaders)
            {
                request.Headers.Remove(k);
                request.Headers.Add(k, v);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            return ApiResult<T>.Fail($"Could not reach the API: {ex.Message}", 0);
        }

        using (response)
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            return Parse<T>(response.StatusCode, content);
        }
    }

    private static ApiResult<T> Parse<T>(HttpStatusCode status, string content)
    {
        var code = (int)status;

        if (status == HttpStatusCode.UnprocessableEntity)
        {
            return ParseGate<T>(content);
        }

        if (!IsSuccess(status))
        {
            return ApiResult<T>.Fail(ExtractError(content, code), code);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return ApiResult<T>.Ok(default);
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Success wrapper: { data, warnings }.
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("data", out var data)
                && root.TryGetProperty("warnings", out var warns))
            {
                var value = data.Deserialize<T>(Json);
                return ApiResult<T>.Ok(value, ReadMessages(warns));
            }

            return ApiResult<T>.Ok(root.Deserialize<T>(Json));
        }
        catch (JsonException ex)
        {
            return ApiResult<T>.Fail($"Unexpected response from the API: {ex.Message}", code);
        }
    }

    private static ApiResult<T> ParseGate<T>(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var overridable = root.TryGetProperty("overridable", out var o) && o.ValueKind == JsonValueKind.True;
            var hardStops = root.TryGetProperty("hardStops", out var hs) ? ReadMessages(hs) : Array.Empty<RuleMessage>();
            var warnings = root.TryGetProperty("warnings", out var w) ? ReadMessages(w) : Array.Empty<RuleMessage>();
            return ApiResult<T>.Gate(overridable, hardStops, warnings);
        }
        catch (JsonException)
        {
            return ApiResult<T>.Fail("The action was blocked by a safety rule.", 422);
        }
    }

    private static IReadOnlyList<RuleMessage> ReadMessages(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RuleMessage>();
        }

        var list = new List<RuleMessage>();
        foreach (var item in element.EnumerateArray())
        {
            var msgCode = item.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : string.Empty;
            var message = item.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
            list.Add(new RuleMessage(msgCode, message));
        }

        return list;
    }

    private static string ExtractError(string content, int code)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"Request failed ({code}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                {
                    return e.GetString()!;
                }

                // ProblemDetails (401/403): prefer detail, then title.
                if (root.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    return d.GetString()!;
                }

                if (root.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    return t.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        return $"Request failed ({code}).";
    }

    private static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;
}
