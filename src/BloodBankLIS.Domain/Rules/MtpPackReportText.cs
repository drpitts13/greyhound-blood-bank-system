namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Statements printed on every MTP pack report. They describe how the system released the units and general
/// handling; they make no clinical or time-limit claim of their own. A time limit appears only when the facility has
/// entered the container's validated hours. The wording REQUIRES SME VERIFICATION before go-live (OCD-045).
/// </summary>
public static class MtpPackReportText
{
    public const string Heading = "MASSIVE TRANSFUSION PROTOCOL — PACK RECORD";

    public static IReadOnlyList<string> Disclaimers(bool anyUncrossmatched, int? validatedHours, bool temperatureOutsideRange)
    {
        var lines = new List<string>
        {
            "These units were released under a massive transfusion protocol. Releasing them was authorized by the blood bank; the decision to transfuse rests with the treating clinician.",
            "Before each unit is hung, confirm the patient's identity against the unit label and its compatibility tag at the bedside.",
            "This record lists what was packed. It does not replace the unit labels or the compatibility tags."
        };

        if (anyUncrossmatched)
        {
            lines.Add("Units marked Uncrossmatched were issued before compatibility testing was complete. The blood bank will complete a crossmatch afterwards and report any incompatibility.");
        }

        if (temperatureOutsideRange)
        {
            lines.Add("The temperature recorded at packing was outside the range set for this container. Contact the blood bank before using these units.");
        }

        if (validatedHours is int hours)
        {
            lines.Add($"Your facility has validated this container for up to {hours} hour{(hours == 1 ? "" : "s")} from the packed time.");
        }

        lines.Add("Keep the container closed between uses. Return unused units and this record to the blood bank as soon as they are no longer needed.");
        return lines;
    }
}
