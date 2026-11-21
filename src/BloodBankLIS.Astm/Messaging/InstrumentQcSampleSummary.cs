using BloodBankLIS.Astm.Parsing;

namespace BloodBankLIS.Astm.Messaging;

/// <summary>
/// What an instrument reported for a QC sample, read from its result records (end-user testing pass 109: QC samples were
/// kept out of patient results, pass 84, but could be read only by opening the instrument message log, so the tech
/// recording reagent QC could not see what the analyzer ran). Each line is the test as the instrument names it and the
/// reaction or interpretation it sent ("Screen 1: 0", "Screen: Positive"). Nothing is recorded from it.
/// </summary>
public static class InstrumentQcSampleSummary
{
    public static IReadOnlyList<string> Results(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !AstmParser.TryParse(raw, out var message, out _) || message is null)
        {
            return [];
        }

        return message.Records
            .Where(r => r.Type == 'R')
            .Select(r =>
            {
                var test = r.GetComponent(3, 4);
                if (string.IsNullOrWhiteSpace(test))
                {
                    test = r.GetField(3).Trim('^');
                }

                var value = r.GetComponent(4, 1);
                return string.IsNullOrWhiteSpace(test) ? null : $"{test}: {(string.IsNullOrWhiteSpace(value) ? "(no value)" : value)}";
            })
            .OfType<string>()
            .ToList();
    }
}
