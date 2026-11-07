using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.Domain.Rules;

/// <summary>
/// Which code identifies a test in an interface message. A result for a test ordered as part of a grouper is reported
/// under the grouper's procedure code when the grouper has an outbound translation (TNS → LAB10117); otherwise under the
/// test's own translation, or the local test code.
/// </summary>
public static class TestCodeTranslationRule
{
    public const string UnknownCode = "HL7-ORDER-CODE";

    public static string ServiceCode(InterfaceValueTranslator translator, string testCode, string? grouperCode)
    {
        if (!string.IsNullOrWhiteSpace(grouperCode)
            && translator.TryToExternal(InterfaceDataItemKeys.ResultObrTestCode, grouperCode, out var grouperExternal))
        {
            return grouperExternal;
        }

        return translator.ToExternal(InterfaceDataItemKeys.ResultObrTestCode, testCode) ?? testCode;
    }

    public static string UnknownOrderCodeMessage(string code) =>
        $"{UnknownCode}: order code '{code}' has no translation to an active test or test grouper. Add it under Admin › HL7 › Translations (Procedure code), then reprocess the message.";
}
