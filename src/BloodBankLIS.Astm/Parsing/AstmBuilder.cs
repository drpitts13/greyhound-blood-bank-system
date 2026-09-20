using System.Text;

namespace BloodBankLIS.Astm.Parsing;

/// <summary>Builds LIS2-A2 messages with standard H|\^&amp;| delimiters.</summary>
public static class AstmBuilder
{
    public static string Header(string sender, string receiver, DateTime utc) =>
        $"H|\\^&|||{sender}|||||{receiver}|||LIS2-A2|{utc:yyyyMMddHHmmss}";

    public static string Terminator(string code = "N") => $"L|1|{code}";

    public static string Join(params string[] records)
    {
        var sb = new StringBuilder();
        foreach (var record in records)
        {
            sb.Append(record.TrimEnd('\r')).Append('\r');
        }

        return sb.ToString();
    }

    public static string Patient(int sequence, string? mrn, string last, string first, string? dob, string? sex) =>
        $"P|{sequence}|{mrn}|||{last}^{first}||{dob}|{sex}";

    public static string Order(int sequence, string specimenId, string assay, string specimenDescriptor = "Blood^Patient", string action = "") =>
        string.IsNullOrEmpty(action)
            ? $"O|{sequence}|{specimenId}||^^^ {assay}|R||||||||||{specimenDescriptor}".Replace("^^^ ", "^^^")
            : $"O|{sequence}|{specimenId}||^^^ {assay}|R||||||{action}||||{specimenDescriptor}".Replace("^^^ ", "^^^");

    public static string Result(int sequence, string analyte, string value, string status, string? completed, string? instrument) =>
        $"R|{sequence}|^^^{analyte}|{value}|||||{status}|||{completed}|{instrument}";

    public static string Query(int sequence, string accession) =>
        $"Q|{sequence}|{accession}||ALL";

    public static string Comment(int sequence, string source, string text, string? type = null) =>
        type is null ? $"C|{sequence}|{source}|{text}" : $"C|{sequence}|{source}|{text}|{type}";
}
