using Avalonia.Controls;
using MusicVault.App.Localization;

namespace MusicVault.App.Views.Dialogs;

public partial class ConfirmRemoveDownloadDialog : Window
{
    public ConfirmRemoveDownloadDialog() => InitializeComponent();

    public ConfirmRemoveDownloadDialog(string displayName, bool canDeleteFile) : this()
    {
        Title = TransferRowLabels.RemoveRecordModalTitle;
        ModalTitleText.Text = TransferRowLabels.RemoveRecordModalTitle;
        CancelButton.Content = TransferRowLabels.ModalCancel;
        SummaryText.Text = TransferRowLabels.ClearSelectedTitleOne(displayName);

        if (canDeleteFile)
        {
            DetailText.Text = TransferRowLabels.ClearSelectedDescriptionWithFiles;
            ListOnlyButton.Content = TransferRowLabels.ClearSelectedConfirmListOnly;
            ListAndFileButton.Content = TransferRowLabels.RemoveRecordListAndFile;
            ListOnlyButton.IsVisible = true;
            ListAndFileButton.IsVisible = true;
        }
        else
        {
            DetailText.Text = TransferRowLabels.ClearSelectedDescriptionListOnly;
            ListOnlyButton.Content = TransferRowLabels.ClearSelectedConfirmDefault;
            ListOnlyButton.Classes.Add("primary");
            ListOnlyButton.Classes.Remove("flat-btn");
            ListAndFileButton.IsVisible = false;
        }

        CancelButton.Click += (_, _) => Close(RemoveDownloadChoice.Cancel);
        ListOnlyButton.Click += (_, _) => Close(RemoveDownloadChoice.ListOnly);
        ListAndFileButton.Click += (_, _) => Close(RemoveDownloadChoice.ListAndFile);
    }
}
