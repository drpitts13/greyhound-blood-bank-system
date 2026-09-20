namespace BloodBankLIS.Domain.Instruments;

/// <summary>Stable catalog keys for ASTM instrument field mapping.</summary>
public static class InstrumentDataItemKeys
{
    public const string PatientId = "patient_id";
    public const string PatientLname = "patient_lname";
    public const string PatientFname = "patient_fname";
    public const string PatientGender = "patient_gender";
    public const string PatientDob = "patient_dob";
    public const string OrderId = "order_id";
    public const string SpecimenId = "specimen_id";
    public const string CollectionDt = "coll_dt";
    public const string ResultDt = "result_dt";
    public const string InstrumentId = "instrument_id";
    public const string Error = "error";
    public const string ResultStatus = "result_status";
    public const string TestCode = "test_code";
    public const string SubtestCode = "subtest_code";
    public const string SubtestResult = "subtest_result";
    public const string TestInterp = "test_interp";

    public static readonly IReadOnlyList<string> All =
    [
        PatientId, PatientLname, PatientFname, PatientGender, PatientDob,
        OrderId, SpecimenId, CollectionDt, ResultDt, InstrumentId,
        Error, ResultStatus, TestCode, SubtestCode, SubtestResult, TestInterp
    ];
}
