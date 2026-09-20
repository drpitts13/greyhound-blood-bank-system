namespace BloodBankLIS.Domain.Instruments;

public sealed record InstrumentDataItem(string Key, string DisplayName, string Description, string DefaultAstmPath, bool Required);

/// <summary>Mappable ASTM data items for instrument interface configuration.</summary>
public static class InstrumentDataItemCatalog
{
    public static readonly IReadOnlyList<InstrumentDataItem> All =
    [
        new(InstrumentDataItemKeys.PatientId, "Patient ID", "Practice-assigned patient identifier (MRN).", "P.3", true),
        new(InstrumentDataItemKeys.PatientLname, "Patient last name", "Patient last name component.", "P.6.1", false),
        new(InstrumentDataItemKeys.PatientFname, "Patient first name", "Patient first name component.", "P.6.2", false),
        new(InstrumentDataItemKeys.PatientGender, "Patient gender", "Patient sex (M/F/U).", "P.9", false),
        new(InstrumentDataItemKeys.PatientDob, "Patient date of birth", "Birth date YYYYMMDD.", "P.8", false),
        new(InstrumentDataItemKeys.OrderId, "Order ID", "LIS order number when present.", "O.4", false),
        new(InstrumentDataItemKeys.SpecimenId, "Specimen ID", "Sample accession number.", "O.3", true),
        new(InstrumentDataItemKeys.CollectionDt, "Collection date/time", "Specimen collection date.", "O.8", false),
        new(InstrumentDataItemKeys.ResultDt, "Result date/time", "Date/time test completed.", "R.13", false),
        new(InstrumentDataItemKeys.InstrumentId, "Instrument ID", "Instrument serial number.", "R.14", false),
        new(InstrumentDataItemKeys.Error, "Error / termination", "Message terminator code or result error.", "L.3", false),
        new(InstrumentDataItemKeys.ResultStatus, "Result status", "ASTM result status (F, P, X, …).", "R.9", false),
        new(InstrumentDataItemKeys.TestCode, "Test code", "Instrument assay / battery code.", "O.5.4", true),
        new(InstrumentDataItemKeys.SubtestCode, "Subtest code", "Instrument analyte code.", "R.3.4", true),
        new(InstrumentDataItemKeys.SubtestResult, "Subtest result", "Original reaction or measured value.", "R.4.1", true),
        new(InstrumentDataItemKeys.TestInterp, "Test interpretation", "Assay-level interpretation (analyte equals assay).", "R.4.1", false)
    ];

    public static InstrumentDataItem? Find(string key) =>
        All.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
}
