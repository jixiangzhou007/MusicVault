namespace MusicVault.App.Services;

public sealed record ToastMessage(Guid Id, MessageLevel Level, string Text, DateTimeOffset CreatedAt);
