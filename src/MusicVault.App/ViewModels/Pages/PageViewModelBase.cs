using System.Windows.Input;

namespace MusicVault.App.ViewModels.Pages;

/// <summary>中央内容区页面 ViewModel 基类</summary>
public abstract class PageViewModelBase : ViewModelBase
{
    public abstract string PageTitle { get; }
    public virtual string PageSubtitle => string.Empty;
    public virtual bool ShowPageSubtitle => !string.IsNullOrWhiteSpace(PageSubtitle);
    /// <summary>为 false 时页面自绘顶栏（如发现页标题+说明同行）。</summary>
    public virtual bool ShowStandardPageHeader => true;
    /// <summary>页头左侧返回/上一级（如艺术家详情返回列表）。</summary>
    public virtual bool ShowPageBackButton => false;
    public virtual string PageBackButtonLabel => string.Empty;
    public virtual ICommand? PageBackCommand => null;
    /// <summary>页头标题区下方的全宽分割线（艺术家详情等自包含页可关闭）。</summary>
    public virtual bool ShowPageHeaderDivider => ShowStandardPageHeader;
}
