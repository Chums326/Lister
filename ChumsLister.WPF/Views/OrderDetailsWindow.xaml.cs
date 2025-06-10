using ChumsLister.Core.Services;
using System.Windows;

namespace ChumsLister.WPF.Views
{
    public partial class OrderDetailsWindow : Window
    {
        public OrderDetailsWindow(OrderDetails order)
        {
            InitializeComponent();
            DataContext = order;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
