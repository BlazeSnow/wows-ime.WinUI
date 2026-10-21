using wows_ime.Pages.Abstractions;

namespace wows_ime.Pages.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly Uri ProjectWebsiteUri = new("https://wows.blazesnow.com/");
    private static readonly Uri ProjectRepositoryUri = new("https://github.com/BlazeSnow/wows-ime.WinUI");
    private IPageHost host = null!;
    private bool suppressSelectionChange;

    public string AppVersionText { get; private set; } = string.Empty;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        host = e.Parameter as IPageHost ?? throw new InvalidOperationException("SettingsPage requires an IPageHost navigation parameter.");
        AppVersionText = host.Window.GetAppVersion();
        Bindings.Update();
        ApplyLocalization();
        SelectLanguage(host.Settings.LoadLanguageMode() ?? "auto");
    }

    private void ApplyLocalization()
    {
        LanguageCard.Header = new TextBlock { Text = SR("Settings/LanguageLabel") };
        AutomaticLanguageItem.Content = SR("Settings/Automatic");
        ProjectWebsiteCard.Header = new TextBlock { Text = SR("ProjectWebsiteCardHeader/Text") };
        ProjectWebsiteCard.Description = "https://wows.blazesnow.com/";
        OpenProjectWebsiteButton.Content = SR("OpenProjectWebsiteButton/Content");
        ProjectRepositoryCard.Header = new TextBlock { Text = SR("ProjectRepositoryCardHeader/Text") };
        ProjectRepositoryCard.Description = "https://github.com/BlazeSnow/wows-ime.WinUI";
        OpenProjectRepositoryButton.Content = SR("OpenProjectRepositoryButton/Content");
        VersionCard.Header = new TextBlock { Text = SR("AppVersionCardHeader/Text") };
    }

    private void SelectLanguage(string? language)
    {
        suppressSelectionChange = true;
        LanguageComboBox.SelectedIndex = language switch { "zh-Hans" => 1, "zh-Hant" => 2, "ja" => 3, _ => 0 };
        suppressSelectionChange = false;
    }

    private async void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSelectionChange || LanguageComboBox.SelectedItem is not ComboBoxItem { Tag: string tag } || tag == (host.Settings.LoadLanguageMode() ?? "auto"))
        {
            return;
        }

        var restart = await ShowLanguageRestartDialogAsync(host.Localization.ResolveLanguage(tag));
        host.Settings.SaveLanguageMode(tag);
        host.Application.SetPrimaryLanguageOverride(tag == "auto" ? string.Empty : tag);
        if (restart)
        {
            host.Application.Restart();
        }
    }

    private async Task<bool> ShowLanguageRestartDialogAsync(string language)
    {
        var dialog = new ContentDialog
        {
            Title = host.Localization.GetString("Settings/Restart/Title", language),
            Content = host.Localization.GetString("Settings/Restart/Content", language),
            PrimaryButtonText = host.Localization.GetString("Settings/Restart/Primary", language),
            CloseButtonText = host.Localization.GetString("Settings/Restart/Close", language),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void OpenProjectWebsiteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await host.Window.LaunchUriAsync(ProjectWebsiteUri))
        {
            await ShowErrorAsync(SR("Status/OpenProjectWebsiteFailed"));
        }
    }

    private async void OpenProjectRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await host.Window.LaunchUriAsync(ProjectRepositoryUri))
        {
            await ShowErrorAsync(SR("Status/OpenProjectRepositoryFailed"));
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog { Title = message, CloseButtonText = SR("Dialog/Common/OK"), XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    private string SR(string key) => host.Localization.GetString(key);
}
