using ChumsLister.Core.Models;
using ChumsLister.Core.Services;   // InventoryRepository lives here
using ChumsLister.WPF.Services;    // InventoryService lives here
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChumsLister.WPF.Views
{
    public partial class POSPage : Page
    {
        // Holds the cart items for this POS session
        private readonly ObservableCollection<InventoryItem> _cartItems = new();

        // We will receive this via DI instead of using a static Instance
        private readonly InventoryService _inventoryService;

        /// <summary>
        /// Constructor now takes an InventoryService (singleton) from DI.
        /// </summary>
        public POSPage(InventoryService inventoryService)
        {
            InitializeComponent();

            // Store the injected instance (never null, because DI registered it)
            _inventoryService = inventoryService
                ?? throw new ArgumentNullException(nameof(inventoryService));

            // Load the inventory once at startup
            _inventoryService.LoadInventory();

            // Bind our DataGrid to the _cartItems collection
            cartDataGrid.ItemsSource = _cartItems;
            cartDataGrid.CellEditEnding += CartDataGrid_CellEditEnding;

            // Default status selection
            statusComboBox.SelectedIndex = 0;
            txtBarcodeInput.Focus();
            UpdateTotal();
        }

        /// <summary>
        /// When the user finishes editing a cell in the cart, re‐compute total.
        /// </summary>
        private void CartDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.InvokeAsync(UpdateTotal, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Handle Enter on the barcode input. Find the matching InventoryItem, add to cart if found.
        /// </summary>
        private void txtBarcodeInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            string barcode = txtBarcodeInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(barcode))
                return;

            // Look in the loaded inventory list for a matching SKU
            var inventoryList = _inventoryService.InventoryItems;
            var item = inventoryList.FirstOrDefault(i =>
                string.Equals(i.SKU?.Trim(), barcode, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                // If it's not already in the cart, add it
                var existing = _cartItems.FirstOrDefault(c => c.SKU == item.SKU);
                if (existing == null)
                {
                    _cartItems.Add(new InventoryItem
                    {
                        SKU = item.SKU,
                        DESCRIPTION = item.DESCRIPTION,
                        RETAIL_PRICE = item.RETAIL_PRICE,
                        STATUS = (statusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "POS"
                    });
                }

                txtBarcodeInput.Clear();
                UpdateTotal();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"Item with SKU '{barcode}' not found.",
                    "Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        /// <summary>
        /// Recalculates the total of all RETAIL_PRICE values in the cart.
        /// </summary>
        private void UpdateTotal()
        {
            decimal total = _cartItems.Sum(i => i.RETAIL_PRICE);
            txtTotal.Text = $"Total: ${total:F2}";
        }

        /// <summary>
        /// When “Checkout” is clicked, update each InventoryItem (decrement stock, record sale),
        /// then clear the cart and reload the inventory from disk/DB.
        /// </summary>
        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0)
            {
                System.Windows.MessageBox.Show("Cart is empty.", "Checkout", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string selectedStatus = (statusComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "POS";
            bool anyFailed = false;

            // For each item in the cart, find its master InventoryItem and update fields
            foreach (var cartItem in _cartItems)
            {
                var inventoryMaster = _inventoryService.InventoryItems
                    .FirstOrDefault(i => i.SKU == cartItem.SKU);

                if (inventoryMaster != null)
                {
                    // Decrement quantity, record qty sold, mark location as "sold"
                    inventoryMaster.QTY_SOLD += 1;
                    inventoryMaster.QTY -= 1;
                    inventoryMaster.LOCATION = "sold";

                    decimal newPrice = cartItem.RETAIL_PRICE;
                    string newDate = DateTime.Now.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

                    // Append new sale date to existing comma-separated DATE_SOLD
                    inventoryMaster.SOLD_PRICE = newPrice;
                    inventoryMaster.DATE_SOLD = AppendValue(inventoryMaster.DATE_SOLD, newDate);
                    inventoryMaster.STATUS = AppendValue(inventoryMaster.STATUS, selectedStatus);

                    try
                    {
                        // Persist changes back to the database (or CSV) via the repository
                        InventoryRepository.UpdateItem(inventoryMaster);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to update {cartItem.SKU}: {ex.Message}");
                        anyFailed = true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"SKU not found in inventory: {cartItem.SKU}");
                    anyFailed = true;
                }
            }

            // Clear the cart UI
            _cartItems.Clear();
            UpdateTotal();

            // Reload the inventory from disk/DB so the in‐memory list is fresh
            _inventoryService.LoadInventory();

            System.Windows.MessageBox.Show(
                anyFailed ? "Some items failed to update." : "Transaction complete. Thank you!",
                "Checkout",
                MessageBoxButton.OK,
                anyFailed ? MessageBoxImage.Warning : MessageBoxImage.Information
            );
        }

        /// <summary>
        /// Utility: if `existing` is empty/null, return `newValue`; otherwise append with comma.
        /// </summary>
        private string AppendValue(string existing, string newValue)
        {
            return string.IsNullOrWhiteSpace(existing)
                ? newValue
                : $"{existing},{newValue}";
        }
    }
}
