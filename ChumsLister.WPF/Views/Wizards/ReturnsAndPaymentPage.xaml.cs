using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added for Mouse and Cursors
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Interaction logic for ReturnsAndPaymentPage.xaml
    /// </summary>
    public partial class ReturnsAndPaymentPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;
        private string _accountId;

        public ReturnsAndPaymentPage(IEbayService ebayService)
        {
            InitializeComponent();
            _ebayService = ebayService;
        }

        public bool ValidatePage()
        {
            // Validate return policy
            if (!rbReturnsAccepted.IsChecked.HasValue ||
                (!rbReturnsAccepted.IsChecked.Value && !rbNoReturns.IsChecked.Value))
            {
                System.Windows.MessageBox.Show("Please select a return policy", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // If returns accepted, validate details
            if (rbReturnsAccepted.IsChecked == true)
            {
                if (cboReturnPeriod.SelectedItem == null)
                {
                    System.Windows.MessageBox.Show("Please select a return period", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (cboRefundType.SelectedItem == null)
                {
                    System.Windows.MessageBox.Show("Please select a refund type", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (!rbBuyerPaysReturn.IsChecked.HasValue ||
                    (!rbBuyerPaysReturn.IsChecked.Value && !rbSellerPaysReturn.IsChecked.Value))
                {
                    System.Windows.MessageBox.Show("Please select who pays for return shipping", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            // If using business policies, validate selections
            if (chkUseBusinessPolicies.IsChecked == true)
            {
                if (cboPaymentPolicy.SelectedItem == null ||
                    cboReturnPolicy.SelectedItem == null ||
                    cboShippingPolicy.SelectedItem == null)
                {
                    System.Windows.MessageBox.Show("Please select all required business policies", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            // Save return policy
            if (rbReturnsAccepted.IsChecked == true)
            {
                listingData.ReturnsAccepted = "ReturnsAccepted";

                var returnPeriod = (cboReturnPeriod.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                listingData.ReturnPeriod = returnPeriod;

                var refundType = (cboRefundType.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                listingData.RefundOption = refundType;

                listingData.ReturnShippingPaidBy = rbBuyerPaysReturn.IsChecked == true
                    ? "Buyer"
                    : "Seller";

                listingData.ReturnPolicyDescription = txtReturnPolicyDetails.Text.Trim();
            }
            else
            {
                listingData.ReturnsAccepted = "ReturnsNotAccepted";
                listingData.ReturnPeriod = null;
                listingData.RefundOption = null;
                listingData.ReturnShippingPaidBy = null;
                listingData.ReturnPolicyDescription = null;
            }

            // Save payment methods
            listingData.PaymentMethods.Clear();

            // eBay Managed Payments includes these by default
            listingData.PaymentMethods.Add("PayPal");
            listingData.PaymentMethods.Add("CreditCard");

            if (chkGooglePay.IsChecked == true)
                listingData.PaymentMethods.Add("GooglePay");

            if (chkApplePay.IsChecked == true)
                listingData.PaymentMethods.Add("ApplePay");

            // Save business policies if using them
            if (chkUseBusinessPolicies.IsChecked == true)
            {
                listingData.PaymentPolicyId = (cboPaymentPolicy.SelectedItem as PolicyItem)?.Id;
                listingData.ReturnPolicyId = (cboReturnPolicy.SelectedItem as PolicyItem)?.Id;
                listingData.ShippingPolicyId = (cboShippingPolicy.SelectedItem as PolicyItem)?.Id;
            }
            else
            {
                listingData.PaymentPolicyId = null;
                listingData.ReturnPolicyId = null;
                listingData.ShippingPolicyId = null;
            }
        }

        public async void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;

            // Load return policy
            if (listingData.ReturnsAccepted == "ReturnsAccepted")
            {
                rbReturnsAccepted.IsChecked = true;

                // Load return period
                if (!string.IsNullOrEmpty(listingData.ReturnPeriod))
                {
                    foreach (ComboBoxItem item in cboReturnPeriod.Items)
                    {
                        if (item.Tag?.ToString() == listingData.ReturnPeriod)
                        {
                            cboReturnPeriod.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Load refund type
                if (!string.IsNullOrEmpty(listingData.RefundOption))
                {
                    foreach (ComboBoxItem item in cboRefundType.Items)
                    {
                        if (item.Tag?.ToString() == listingData.RefundOption)
                        {
                            cboRefundType.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Load who pays return shipping
                if (listingData.ReturnShippingPaidBy == "Buyer")
                    rbBuyerPaysReturn.IsChecked = true;
                else
                    rbSellerPaysReturn.IsChecked = true;

                txtReturnPolicyDetails.Text = listingData.ReturnPolicyDescription ?? "";
            }
            else
            {
                rbNoReturns.IsChecked = true;
            }

            // Load payment methods
            chkGooglePay.IsChecked = listingData.PaymentMethods.Contains("GooglePay");
            chkApplePay.IsChecked = listingData.PaymentMethods.Contains("ApplePay");

            // Load business policies if available
            if (!string.IsNullOrEmpty(listingData.PaymentPolicyId) ||
                !string.IsNullOrEmpty(listingData.ReturnPolicyId) ||
                !string.IsNullOrEmpty(listingData.ShippingPolicyId))
            {
                chkUseBusinessPolicies.IsChecked = true;
                await LoadBusinessPolicies();

                // Select the saved policies
                SelectPolicy(cboPaymentPolicy, listingData.PaymentPolicyId);
                SelectPolicy(cboReturnPolicy, listingData.ReturnPolicyId);
                SelectPolicy(cboShippingPolicy, listingData.ShippingPolicyId);
            }
        }

        private void Returns_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlReturnDetails == null) return;

            pnlReturnDetails.Visibility = rbReturnsAccepted.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void chkUseBusinessPolicies_Checked(object sender, RoutedEventArgs e)
        {
            pnlBusinessPolicies.Visibility = Visibility.Visible;
            await LoadBusinessPolicies();
        }

        private void chkUseBusinessPolicies_Unchecked(object sender, RoutedEventArgs e)
        {
            pnlBusinessPolicies.Visibility = Visibility.Collapsed;
            cboPaymentPolicy.Items.Clear();
            cboReturnPolicy.Items.Clear();
            cboShippingPolicy.Items.Clear();
        }

        private async Task LoadBusinessPolicies()
        {
            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // In a real implementation, these would come from the eBay API
                // For now, we'll simulate with placeholder data
                await Task.Delay(500);

                // Load payment policies
                cboPaymentPolicy.Items.Clear();
                cboPaymentPolicy.Items.Add(new PolicyItem { Id = "1", Name = "Standard Payment Policy" });
                cboPaymentPolicy.Items.Add(new PolicyItem { Id = "2", Name = "Immediate Payment Required" });

                // Load return policies
                cboReturnPolicy.Items.Clear();
                cboReturnPolicy.Items.Add(new PolicyItem { Id = "1", Name = "30 Day Returns" });
                cboReturnPolicy.Items.Add(new PolicyItem { Id = "2", Name = "No Returns" });
                cboReturnPolicy.Items.Add(new PolicyItem { Id = "3", Name = "14 Day Returns" });

                // Load shipping policies
                cboShippingPolicy.Items.Clear();
                cboShippingPolicy.Items.Add(new PolicyItem { Id = "1", Name = "Standard Shipping" });
                cboShippingPolicy.Items.Add(new PolicyItem { Id = "2", Name = "Free Shipping" });
                cboShippingPolicy.Items.Add(new PolicyItem { Id = "3", Name = "Expedited Shipping" });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading business policies: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void SelectPolicy(System.Windows.Controls.ComboBox comboBox, string policyId)
        {
            if (string.IsNullOrEmpty(policyId)) return;

            foreach (PolicyItem item in comboBox.Items)
            {
                if (item.Id == policyId)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private class PolicyItem
        {
            public string Id { get; set; }
            public string Name { get; set; }

            public override string ToString()
            {
                return Name;
            }
        }
    }
}