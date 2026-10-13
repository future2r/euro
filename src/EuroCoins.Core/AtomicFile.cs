using System.Text;

namespace EuroCoins.Core;

/// <summary>
/// Writes files so that readers (and sync clients such as OneDrive) never see a half-written file.
/// </summary>
internal static class AtomicFile
{
    /// <summary>Writes UTF-8 text (without BOM) to a temporary file and then replaces the target.</summary>
    public static void WriteAllText(string path, string text)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temp, path, overwrite: true);
    }
}
