using BloodBankLIS.Application.Admin;

namespace BloodBankLIS.Application.Abstractions;

/// <summary>
/// Optional drop folder of facility-exported ICCBBA extracts. Empty or missing
/// directories are a no-op — placeholders stay until an administrator imports.
/// </summary>
public interface IIccbbaExtractDirectory
{
    string ResolvedPath { get; }

    IReadOnlyList<IccbbaExtractFileDto> ListAvailable();

    bool TryRead(string fileName, out string content, out string error);
}
