using MusicVault.Core.Models;

namespace MusicVault.Services.Organize;

/// <summary>文件重命名与目录整理，支持模板预览</summary>
public sealed class FileOrganizerService
{
    public string ApplyTemplate(string template, Track track, string extension = ".mp3")
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return template
            .Replace("{Title}", Sanitize(track.Title))
            .Replace("{Artist}", Sanitize(track.Artist))
            .Replace("{Album}", Sanitize(track.Album))
            .Replace("{AlbumArtist}", Sanitize(track.AlbumArtist))
            .Replace("{TrackNumber}", track.TrackNumber?.ToString("D2") ?? "00")
            .Replace("{Year}", track.Year?.ToString() ?? "")
            .Replace("{Genre}", Sanitize(track.Genre))
            .Replace("{Extension}", ext.TrimStart('.'));
    }

    public OrganizePreview PreviewOrganize(Track track, string rootPath, string? folderTemplate, string? fileNameTemplate)
    {
        var ext = Path.GetExtension(track.FilePath);
        var sourceDir = Path.GetDirectoryName(track.FilePath) ?? rootPath;

        var targetDir = string.IsNullOrWhiteSpace(folderTemplate)
            ? sourceDir
            : Path.Combine(rootPath, ApplyTemplate(folderTemplate, track, ext));

        var fileName = string.IsNullOrWhiteSpace(fileNameTemplate)
            ? Path.GetFileName(track.FilePath)
            : ApplyTemplate(fileNameTemplate, track, ext)
              + (fileNameTemplate.Contains("{Extension}", StringComparison.Ordinal) ? "" : ext);

        var targetPath = Path.Combine(targetDir, fileName);
        return new OrganizePreview(track.FilePath, targetPath, targetDir, fileName);
    }

    public async Task<bool> ExecuteOrganizeAsync(OrganizePreview preview, bool move, CancellationToken ct = default)
    {
        if (string.Equals(preview.SourcePath, preview.TargetPath, StringComparison.OrdinalIgnoreCase))
            return true;

        Directory.CreateDirectory(preview.TargetDirectory);
        if (move)
            File.Move(preview.SourcePath, preview.TargetPath, overwrite: false);
        else
        {
            await using var src = File.OpenRead(preview.SourcePath);
            await using var dst = File.Create(preview.TargetPath);
            await src.CopyToAsync(dst, ct);
        }
        return true;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}

public sealed record OrganizePreview(string SourcePath, string TargetPath, string TargetDirectory, string FileName);
