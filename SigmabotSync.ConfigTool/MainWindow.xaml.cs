using System.Windows;
using SigmabotSync.ConfigTool.ViewModels;

namespace SigmabotSync.ConfigTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
