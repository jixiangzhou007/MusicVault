using MusicVault.Core.Models;

namespace MusicVault.App.ViewModels.Player;

public sealed class TrackFilePathChangedEventArgs : EventArgs
{
    public TrackFilePathChangedEventArgs(Track track, string oldPath, string newPath)
    {
        Track = track;
        OldPath = oldPath;
        NewPath = newPath;
    }

    public Track Track { get; }
    public string OldPath { get; }
    public string NewPath { get; }
}
