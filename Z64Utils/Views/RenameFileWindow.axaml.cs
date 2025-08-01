using Avalonia.Controls;
using Avalonia.Interactivity;
using Z64Utils.ViewModels;

namespace Z64Utils.Views;

public partial class RenameFileWindow : Window
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public RenameFileWindow()
    {
        InitializeComponent();

        // Focus the segment id text box (and the dialog)
        Opened += (sender, e) =>
        {
            // Select the content of the text box on focus
            void OnNameTextBoxGotFocus(object? sender, RoutedEventArgs args)
            {
                Logger.Trace("OnNameTextBoxGotFocus");
                NameTextBox.SelectAll();
            }
            NameTextBox.GotFocus += OnNameTextBoxGotFocus;

            NameTextBox.Focus();
        };
    }

    public void OnOKButtonClick(object? sender, RoutedEventArgs args)
    {
        var vm = (RenameFileWindowViewModel?)DataContext;
        Close(vm?.Name);
    }
}
