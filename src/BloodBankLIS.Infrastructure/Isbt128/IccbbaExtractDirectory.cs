using BloodBankLIS.Application.Abstractions;
using BloodBankLIS.Application.Admin;
using Microsoft.Extensions.Configuration;

namespace BloodBankLIS.Infrastructure.Isbt128;

/// <summary>
/// Lists and reads CSV/TSV/JSON extracts from <c>Iccbba:ExtractDirectory</c>.
/// Does not traverse subfolders or invent ICCBBA codes.
/// </summary>
public sealed class IccbbaExtractDirectory : IIccbbaExtractDirectory
{
    public const string ConfigurationKey = "Iccbba:ExtractDirectory";
    public const string DefaultRelativePath = "testdata/isbt128/extracts";

    public IccbbaExtractDirectory(string resolvedPath)
    {
        ResolvedPath = resolvedPath;
    }

    public string ResolvedPath { get; }

    public static IccbbaExtractDirectory FromConfiguration(IConfiguration? configuration, string? contentRoot = null)
    {
        var configured = configuration?[ConfigurationKey];
        return new IccbbaExtractDirectory(ResolvePath(configured, contentRoot));
    }

    public static string ResolvePath(string? configured, string? contentRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(Path.Combine(
                    string.IsNullOrWhiteSpace(contentRoot) ? Directory.GetCurrentDirectory() : contentRoot,
                    configured));
        }

        foreach (var start in StartDirectories(contentRoot))
        {
            for (var dir = start; dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "testdata", "isbt128", "extracts");
                if (Directory.Exists(candidate) || File.Exists(Path.Combine(dir.FullName, "BloodBankLIS.slnx")))
                    return Path.GetFullPath(candidate);
            }
        }

        return Path.GetFullPath(Path.Combine(
            string.IsNullOrWhiteSpace(contentRoot) ? Directory.GetCurrentDirectory() : contentRoot,
            DefaultRelativePath));
    }

    private static IEnumerable<DirectoryInfo> StartDirectories(string? contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(contentRoot))
            yield return new DirectoryInfo(contentRoot);
        yield return new DirectoryInfo(Directory.GetCurrentDirectory());
        yield return new DirectoryInfo(AppContext.BaseDirectory);
    }

    public IReadOnlyList<IccbbaExtractFileDto> ListAvailable()
    {
        if (!Directory.Exists(ResolvedPath))
            return [];

        return Directory.GetFiles(ResolvedPath)
            .Select(Path.GetFileName)
            .Where(name => name is not null && IccbbaExtractParser.IsAllowedExtractExtension(name))
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var full = Path.Combine(ResolvedPath, name);
                var info = new FileInfo(full);
                var kind = IccbbaExtractParser.Classify(name);
                return new IccbbaExtractFileDto(name, info.Length, kind.ToString(), ResolvedPath);
            })
            .ToList();
    }

    public bool TryRead(string fileName, out string content, out string error)
    {
        content = string.Empty;
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOfAny(['/', '\\']) >= 0
            || fileName.Contains("..", StringComparison.Ordinal)
            || Path.GetFileName(fileName) != fileName)
        {
            error = "Extract file name must be a file in the configured drop folder.";
            return false;
        }

        if (!IccbbaExtractParser.IsAllowedExtractExtension(fileName)
            || IccbbaExtractParser.IsUnsupportedNativeDatabase(fileName))
        {
            error = IccbbaExtractParser.IsUnsupportedNativeDatabase(fileName)
                ? IccbbaExtractParser.NativeDatabaseMessage
                : "Only CSV, TSV, TXT, or JSON extract files can be imported.";
            return false;
        }

        var full = Path.GetFullPath(Path.Combine(ResolvedPath, fileName));
        var root = Path.GetFullPath(ResolvedPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            error = "Extract file name must be a file in the configured drop folder.";
            return false;
        }

        if (!File.Exists(full))
        {
            error = $"Extract file '{fileName}' was not found in the drop folder.";
            return false;
        }

        content = File.ReadAllText(full);
        error = string.Empty;
        return true;
    }
}
