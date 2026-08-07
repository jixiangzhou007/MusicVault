using MusicVault.Core.Enums;

namespace MusicVault.App.Localization;

/// <summary>应用 UI 字符串本地化（zh-CN / en-US）。</summary>
public interface ILocalizationService
{
    /// <summary>当前文化代码（zh-CN 或 en-US）。</summary>
    string Culture { get; }

    /// <summary>文化切换后通知各 ViewModel 刷新绑定文案。</summary>
    event EventHandler? CultureChanged;

    /// <summary>获取本地化字符串；缺失时回退为 key。</summary>
    string Get(string key);

    /// <summary>在线平台显示名（随当前文化）。</summary>
    string GetPlatformLabel(MusicPlatform platform);

    /// <summary>切换语言并持久化到 <see cref="Services.AppSettings.Culture"/>。</summary>
    Task SetCultureAsync(string culture, CancellationToken ct = default);
}
