using System.Text;

namespace Phichat.Infrastructure.Files;

/// <summary>Turns an untrusted client file name into a safe single path segment.</summary>
public static class FileNameSanitizer
{
    private const int MaxLength = 100;
    private static readonly HashSet<char> Invalid = new(Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }));

    public static string Sanitize(string? fileName)
    {
        // Strip any directory part, whichever separator the client used.
        var name = (fileName ?? string.Empty).Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch) || Invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }

        name = sb.ToString().Trim().Trim('.').Trim();

        if (name.Length > MaxLength)
        {
            var ext = Path.GetExtension(name);
            if (ext.Length > 20) ext = string.Empty;
            name = name[..(MaxLength - ext.Length)] + ext;
        }

        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }
}
