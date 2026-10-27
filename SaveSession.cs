using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Holds the opened save and its original file name; edits are exported to a new file, never written over the source.</summary>
public sealed class SaveSession(SaveFile sav, string fileName)
{
    public SaveFile Sav { get; } = sav;
    public string FileName { get; } = fileName;

    public static async Task<SaveSession?> OpenAsync(FileResult file)
    {
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return SaveUtil.TryGetSaveFile(ms.ToArray(), out var sav, file.FileName) ? new(sav, file.FileName) : null;
    }

    /// <summary>Writes the edited save (DSV footer preserved) into the cache and opens the Android share sheet.</summary>
    public async Task ExportAsync()
    {
        var name = Path.GetFileNameWithoutExtension(FileName) + "-edited" + Path.GetExtension(FileName);
        var path = Path.Combine(FileSystem.CacheDirectory, name);
        await File.WriteAllBytesAsync(path, Sav.Write().ToArray());
        await Share.RequestAsync(new ShareFileRequest("Export save", new ShareFile(path)));
    }
}
