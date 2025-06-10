using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class ShippingServiceDialog : Window
    {
        public ShippingService SelectedService { get; private set; }
        public decimal Cost { get; private set; }
        public decimal AdditionalCost { get; private set; }
        public bool FreeShipping { get; private set; }

        public ShippingServiceDialog(List<ShippingService> services, string title)
        {
            InitializeComponent();
            Title = title;
            DataContext = this;

            listServices.ItemsSource = services;
            if (services.Count > 0)
            {
                listServices.SelectedIndex = 0;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (listServices.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Please select a shipping service", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!FreeShipping)
            {
                if (!decimal.TryParse(txtCost.Text, out decimal cost) || cost < 0)
                {
                    System.Windows.MessageBox.Show("Please enter a valid shipping cost", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Cost = cost;

                if (!decimal.TryParse(txtAdditionalCost.Text, out decimal additionalCost) || additionalCost < 0)
                {
                    System.Windows.MessageBox.Show("Please enter a valid additional item cost", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                AdditionalCost = additionalCost;
            }
            else
            {
                Cost = 0;
                AdditionalCost = 0;
            }

            SelectedService = listServices.SelectedItem as ShippingService;
            FreeShipping = chkFreeShipping.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void chkFreeShipping_Checked(object sender, RoutedEventArgs e)
        {
            txtCost.IsEnabled = false;
            txtAdditionalCost.IsEnabled = false;
            txtCost.Text = "0.00";
            txtAdditionalCost.Text = "0.00";
        }

        private void chkFreeShipping_Unchecked(object sender, RoutedEventArgs e)
        {
            txtCost.IsEnabled = true;
            txtAdditionalCost.IsEnabled = true;
        }
    }
}