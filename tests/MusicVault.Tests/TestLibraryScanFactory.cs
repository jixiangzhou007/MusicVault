using MusicVault.Core.Interfaces;
using MusicVault.Data.Repositories;
using MusicVault.Services.Library;
using MusicVault.Services.Metadata;

namespace MusicVault.Tests;

internal static class TestLibraryScanFactory
{
    public static LibraryScanService Create(
        TrackRepository tracks,
        LibraryRepository libraries,
        IMetadataService metadata)
    {
        var webDav = new WebDavClient();
        var webDavLibraries = new WebDavLibraryService(libraries, new NullNasCredentialStore(), webDav);
        return new LibraryScanService(tracks, libraries, metadata, webDav, webDavLibraries);
    }
}
