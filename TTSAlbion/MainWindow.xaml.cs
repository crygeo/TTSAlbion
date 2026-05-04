using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls;
using TTSAlbion.ViewModel;

namespace TTSAlbion;

/// <summary>
/// Code-behind mínimo: solo inicializa DataContext.
/// Toda la lógica vive en MainViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void UIElement_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    
    // ================================
// Window chrome behavior
// ================================
    private void WindowMinimize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
