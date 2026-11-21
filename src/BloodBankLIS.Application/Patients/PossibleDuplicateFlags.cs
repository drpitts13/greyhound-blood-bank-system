namespace BloodBankLIS.Application.Patients;

/// <summary>
/// End-user testing pass 248: the Patients list linked to Possible duplicates without saying a pair was waiting, and the
/// row for Demo-Multi, Riley (MRN0022) gave no sign that MRN9022 may be the same person. The list now counts the pairs
/// and marks each chart in one with the other chart's MRN.
/// </summary>
public static class PossibleDuplicateFlags
{
    /// <summary>For each chart in a pair, the MRNs of the charts it may duplicate.</summary>
    public static IReadOnlyDictionary<long, IReadOnlyList<string>> OthersByPatient(IEnumerable<PossibleDuplicatePairDto> pairs)
    {
        var others = new Dictionary<long, List<string>>();
        void Add(PatientDto chart, PatientDto other)
        {
            if (!others.TryGetValue(chart.Id, out var list))
            {
                others[chart.Id] = list = new List<string>();
            }

            if (!list.Contains(other.MedicalRecordNumber, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(other.MedicalRecordNumber);
            }
        }

        foreach (var pair in pairs)
        {
            Add(pair.First, pair.Second);
            Add(pair.Second, pair.First);
        }

        return others.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>The link to the Possible duplicates report, with how many pairs are waiting.</summary>
    public static string LinkText(int pairs) =>
        pairs switch
        {
            0 => "Possible duplicates",
            1 => "Possible duplicates (1 pair to review)",
            _ => $"Possible duplicates ({pairs} pairs to review)"
        };

    /// <summary>The badge on a chart's row: "Possible duplicate of MRN9022".</summary>
    public static string Badge(IReadOnlyList<string> otherMrns) =>
        $"Possible duplicate of {string.Join(", ", otherMrns)}";
}
