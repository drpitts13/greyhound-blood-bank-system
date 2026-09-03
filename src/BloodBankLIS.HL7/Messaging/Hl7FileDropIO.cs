using BloodBankLIS.Domain.Interfaces;

namespace BloodBankLIS.HL7.Messaging;

/// <summary>Filesystem helpers for file-drop HL7 transport.</summary>
public static class Hl7FileDropIO
{
    public static void EnsureLayout(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, Hl7FileDropLayout.ProcessedFolder));
        Directory.CreateDirectory(Path.Combine(root, Hl7FileDropLayout.ErrorFolder));
        Directory.CreateDirectory(Path.Combine(root, Hl7FileDropLayout.AckFolder));
    }

    public static IReadOnlyList<string> ListInbox(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.GetFiles(root)
            .Where(f => Hl7FileDropLayout.IsInboxFileName(Path.GetFileName(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void WriteOutbound(string root, string fileName, string payload)
    {
        EnsureLayout(root);
        var dest = Path.Combine(root, Path.GetFileName(fileName));
        var temp = dest + ".tmp";
        File.WriteAllText(temp, payload);
        File.Move(temp, dest, overwrite: true);
    }

    public static void WriteAck(string root, string sourceFileName, string ackPayload)
    {
        EnsureLayout(root);
        var dest = Path.Combine(root, Hl7FileDropLayout.AckFolder, Hl7FileDropLayout.AckFileName(sourceFileName));
        File.WriteAllText(dest, ackPayload);
    }

    public static void ArchiveProcessed(string root, string sourcePath) =>
        MoveInto(root, Hl7FileDropLayout.ProcessedFolder, sourcePath);

    public static void ArchiveError(string root, string sourcePath) =>
        MoveInto(root, Hl7FileDropLayout.ErrorFolder, sourcePath);

    private static void MoveInto(string root, string folder, string sourcePath)
    {
        EnsureLayout(root);
        var destDir = Path.Combine(root, folder);
        var dest = Path.Combine(destDir, Path.GetFileName(sourcePath));
        if (File.Exists(dest))
        {
            dest = Path.Combine(destDir, $"{Path.GetFileNameWithoutExtension(sourcePath)}_{DateTime.UtcNow:HHmmssfff}{Path.GetExtension(sourcePath)}");
        }

        File.Move(sourcePath, dest, overwrite: false);
    }
}
