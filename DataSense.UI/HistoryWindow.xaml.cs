using System.Windows;
using DataSense.UI.ViewModels;

namespace DataSense.UI
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow(HistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.LoadAsync();
        }
    }
}
