using System.Windows;
using System.Windows.Input;
using MlsmoonSkillManager.App.Theming;
using MlsmoonSkillManager.App.ViewModels;

namespace MlsmoonSkillManager.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        SourceInitialized += (_, _) => NativeWindow.ApplyChrome(this);
        ThemeManager.Changed += OnThemeChanged;
        Closed += (_, _) => ThemeManager.Changed -= OnThemeChanged;
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += async (_, _) => await vm.RefreshAsync().ConfigureAwait(true);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => NativeWindow.ApplyCaptionTheme(this);

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainViewModel vm)
        {
            return;
        }

        if (vm.IsLogOpen)
        {
            vm.CloseLogCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (vm.IsConflictOpen)
        {
            vm.CloseConflictCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (vm.IsSettingsOpen)
        {
            vm.CloseSettingsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void RootChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OnRootsChanged();
        }
    }
}
