using System.Diagnostics;
using MusicVault.Core.Models;
using MusicVault.Data.Database;
using MusicVault.Data.Repositories;

namespace MusicVault.Tests;

/// <summary>大曲库查询耗时回归；本地可配合 scripts/benchmark-library.ps1 查看输出。</summary>
public sealed class LibraryQueryBenchmarkTests
{
    [Fact]
    public async Task SearchScoped_5000_tracks_completes_within_budget()
    {
        var root = Path.Combine(Path.GetTempPath(), "mv-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "musicvault.db");

        try
        {
            await DatabaseInitializer.InitializeAsync(dbPath);
            var tracks = new TrackRepository(dbPath);
            var libraries = new LibraryRepository(dbPath);
            var libraryId = await libraries.AddAsync(new MusicLibrary
            {
                Name = "Bench",
                RootPath = root,
                IsEnabled = true
            });
            var batch = new List<Track>(5000);
            for (var i = 0; i < 5000; i++)
            {
                batch.Add(new Track
                {
                    LibraryId = libraryId,
                    Title = $"Song {i:D4}",
                    Artist = i % 2 == 0 ? "Alpha Band" : "Beta Artist",
                    Album = "Bench Album",
                    FilePath = Path.Combine(root, $"t{i}.mp3"),
                    AddedAt = DateTime.UtcNow
                });
            }

            foreach (var t in batch)
                await tracks.UpsertAsync(t);

            var sw = Stopwatch.StartNew();
            var found = await tracks.SearchScopedAsync("Alpha", libraryId: libraryId, take: 200);
            sw.Stop();

            Assert.True(found.Count > 0);
            Assert.True(sw.ElapsedMilliseconds < 1500,
                $"SearchScoped over 5000 tracks took {sw.ElapsedMilliseconds} ms (budget 1500 ms).");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
        }
    }
}
