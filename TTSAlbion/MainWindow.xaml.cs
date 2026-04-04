using MahApps.Metro.Controls;
using TTSAlbion.ViewModels;

namespace TTSAlbion;

/// <summary>
/// Code-behind mínimo: solo inicializa DataContext.
/// Toda la lógica vive en MainViewModel.
/// </summary>
public partial class MainWindow : MetroWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}