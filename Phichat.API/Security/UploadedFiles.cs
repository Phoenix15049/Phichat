namespace Phichat.API.Security;

/// <summary>Stored message attachments (their URLs look like <c>/uploads/{name}</c>).</summary>
public static class UploadedFiles
{
    public static string Root => Path.Combine(Directory.GetCurrentDirectory(), "Uploads");

    /// <summary>Removes a stored attachment; a file that cannot be removed is left behind, it is not an error.</summary>
    public static void TryDelete(string fileUrl)
    {
        const string prefix = "/uploads/";
        if (!fileUrl.StartsWith(prefix, StringComparison.Ordinal)) return;

        // Only a plain file name directly in the folder, however the URL was written.
        var name = Path.GetFileName(Uri.UnescapeDataString(fileUrl[prefix.Length..]));
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            File.Delete(Path.Combine(Root, name));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
