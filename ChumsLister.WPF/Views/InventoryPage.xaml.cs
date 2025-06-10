using ChumsLister.Core.Models;
using ChumsLister.Core.Services;
using ChumsLister.WPF.Services;
using ExcelDataReader;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ChumsLister.WPF.Views
{
    public partial class InventoryPage : Page
    {
        // Parameterless ctor for XAML
        public InventoryPage()
            : this(App.ServiceProvider.GetRequiredService<InventoryService>())
        {
        }

        private readonly InventoryService _inventoryService;
        private ObservableCollection<InventoryItem> _items;
        private bool _isRestoring;
        private static InventoryPageState _savedState = new InventoryPageState();

        class InventoryPageState
        {
            public int SelectedIndex { get; set; } = -1;
            public double ScrollPos { get; set; }
            public string SearchText { get; set; } = string.Empty;
            public string SelectedSku { get; set; }
            public string SortColumn { get; set; }
            public ListSortDirection? SortDirection { get; set; }
        }

        public InventoryPage(InventoryService inventoryService)
        {
            InitializeComponent();
            _inventoryService = inventoryService;
            inventoryDataGrid.LoadingRow += InventoryDataGrid_LoadingRow;
            inventoryDataGrid.Sorting += InventoryDataGrid_Sorting;
            LoadInventory();
        }

        private void InventoryDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (!_isRestoring)
            {
                _savedState.SortColumn = e.Column.SortMemberPath;
                _savedState.SortDirection = e.Column.SortDirection;
            }
        }

        public static void ClearStaticInventory() => _savedState = new InventoryPageState();
        public static void ForceReloadForUserChange() => _savedState = new InventoryPageState();
        public void OnNavigatingFrom() => SaveState();

        private void LoadInventory()
        {
            _inventoryService.LoadInventory();
            _items = _inventoryService.InventoryItems;
            inventoryDataGrid.ItemsSource = _items;
            Dispatcher.BeginInvoke(new Action(RestoreState), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RestoreState()
        {
            if (_items == null || _items.Count == 0) return;
            _isRestoring = true;
            try
            {
                if (!string.IsNullOrEmpty(_savedState.SearchText))
                {
                    txtSearch.Text = _savedState.SearchText;
                    ApplyFilter(_savedState.SearchText);
                }
                if (!string.IsNullOrEmpty(_savedState.SortColumn) && _savedState.SortDirection.HasValue)
                {
                    var col = inventoryDataGrid.Columns.FirstOrDefault(c => c.SortMemberPath == _savedState.SortColumn);
                    if (col != null)
                    {
                        col.SortDirection = _savedState.SortDirection;
                        var view = CollectionViewSource.GetDefaultView(inventoryDataGrid.ItemsSource) as CollectionView;
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new SortDescription(_savedState.SortColumn, _savedState.SortDirection.Value));
                        view.Refresh();
                    }
                }
                Dispatcher.BeginInvoke(new Action(RestoreSelection), System.Windows.Threading.DispatcherPriority.Background);
            }
            finally { _isRestoring = false; }
        }

        private void RestoreSelection()
        {
            if (!string.IsNullOrEmpty(_savedState.SelectedSku))
            {
                var itm = _items.FirstOrDefault(x => x.SKU == _savedState.SelectedSku);
                if (itm != null)
                {
                    inventoryDataGrid.SelectedItem = itm;
                    inventoryDataGrid.ScrollIntoView(itm);
                }
            }
            else if (_savedState.SelectedIndex >= 0 && _savedState.SelectedIndex < _items.Count)
            {
                inventoryDataGrid.SelectedIndex = _savedState.SelectedIndex;
                inventoryDataGrid.ScrollIntoView(inventoryDataGrid.SelectedItem);
            }
            if (_savedState.ScrollPos > 0)
            {
                var sv = GetScrollViewer(inventoryDataGrid);
                sv?.ScrollToVerticalOffset(_savedState.ScrollPos);
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject dep)
        {
            if (dep is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(dep); i++)
            {
                var child = VisualTreeHelper.GetChild(dep, i);
                var res = GetScrollViewer(child);
                if (res != null) return res;
            }
            return null;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            SaveState();
            LoadInventory();
        }

        private void SaveState()
        {
            _savedState.SearchText = txtSearch.Text;
            _savedState.SelectedIndex = inventoryDataGrid.SelectedIndex;
            _savedState.SelectedSku = (inventoryDataGrid.SelectedItem as InventoryItem)?.SKU;
            var sv = GetScrollViewer(inventoryDataGrid);
            if (sv != null) _savedState.ScrollPos = sv.VerticalOffset;
            if (CollectionViewSource.GetDefaultView(inventoryDataGrid.ItemsSource) is CollectionView view && view.SortDescriptions.Any())
            {
                var sd = view.SortDescriptions[0];
                _savedState.SortColumn = sd.PropertyName;
                _savedState.SortDirection = sd.Direction;
            }
        }

        private void btnAddItem_Click(object sender, RoutedEventArgs e)
        {
            var sku = Guid.NewGuid().ToString("N").Substring(0, 10);
            var newItem = new InventoryItem { SKU = sku, DESCRIPTION = "New Item", QTY = 0 };
            _inventoryService.AddItem(newItem);
            LoadInventory();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (inventoryDataGrid.SelectedItem is InventoryItem it &&
                System.Windows.MessageBox.Show($"Delete {it.SKU}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _inventoryService.RemoveItem(it.SKU);
                LoadInventory();
            }
        }

        private async void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel/CSV (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|All files (*.*)|*.*",
                Title = "Import Inventory"
            };

            if (dialog.ShowDialog() == true)
            {
                // Show spinner
                loadingSpinner.Visibility = Visibility.Visible;

                // Disable buttons during import
                btnImport.IsEnabled = false;
                btnRefresh.IsEnabled = false;
                btnAddItem.IsEnabled = false;
                btnDelete.IsEnabled = false;
                btnExport.IsEnabled = false;

                try
                {
                    // Run import on background thread
                    await Task.Run(() => ImportFromExcel(dialog.FileName));

                    // Reload inventory on UI thread
                    await Dispatcher.InvokeAsync(() => LoadInventory());
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Hide spinner and re-enable buttons
                    loadingSpinner.Visibility = Visibility.Collapsed;
                    btnImport.IsEnabled = true;
                    btnRefresh.IsEnabled = true;
                    btnAddItem.IsEnabled = true;
                    btnDelete.IsEnabled = true;
                    btnExport.IsEnabled = true;
                }
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Export Inventory"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using var fs = File.Open(dialog.FileName, FileMode.Create, FileAccess.Write);
                    using var writer = new StreamWriter(fs, Encoding.UTF8);
                    writer.WriteLine("SKU,Description,Quantity");
                    foreach (var item in _items)
                        writer.WriteLine($"{EscapeForCsv(item.SKU)},{EscapeForCsv(item.DESCRIPTION)},{item.QTY}");
                    System.Windows.MessageBox.Show($"Exported to {dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportFromExcel(string filePath)
        {
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var table = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            }).Tables[0];

            InventoryRepository.ClearInventory();

            // Create case-insensitive column mapping
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var colName = table.Columns[i].ColumnName.Trim();
                map[colName] = i;

                // Also add without spaces/special chars for easier matching
                map[colName.Replace(" ", "").Replace("/", "")] = i;
                map[colName.Replace(" ", "_")] = i;
            }

            // Debug: Print column names
            System.Diagnostics.Debug.WriteLine("=== Excel Columns ===");
            for (int i = 0; i < table.Columns.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Column {i}: '{table.Columns[i].ColumnName}'");
            }

            int totalRows = table.Rows.Count;
            int processedRows = 0;

            foreach (DataRow row in table.Rows)
            {
                var item = new InventoryItem
                {
                    // Column A or "SKU"
                    SKU = GetValue(row, map, "SKU", "A"),

                    // Column B or "Trans ID" 
                    TRANS_ID = GetValue(row, map, "Trans ID", "TRANS_ID", "B"),

                    // Column C or "MODEL/HD SKU" - with forward slash
                    MODEL_HD_SKU = GetValue(row, map, "MODEL/HD SKU", "Model/HD SKU", "MODEL_HD_SKU", "C"),

                    // Column D or "Description"
                    DESCRIPTION = GetValue(row, map, "Description", "DESCRIPTION", "D"),

                    // Column E or "Qty"
                    QTY = GetIntValue(row, map, "Qty", "QTY", "E"),

                    // Column F or "Retail Price"
                    RETAIL_PRICE = GetDecimalValue(row, map, "Retail Price", "RETAIL_PRICE", "F"),

                    // Column G or "Cost/Item" or "Cost Item"
                    COST_ITEM = GetDecimalValue(row, map, "Cost/Item", "Cost Item", "CostItem", "COST_ITEM", "G"),

                    // Column H or "Total Cost/Item" or "Total Cost Item"
                    TOTAL_COST_ITEM = GetDecimalValue(row, map, "Total Cost/Item", "Total Cost Item", "TotalCostItem", "TOTAL_COST_ITEM", "H"),

                    // Column I or "Qty Sold"
                    QTY_SOLD = GetIntValue(row, map, "Qty Sold", "QTY_SOLD", "QtySold", "I"),

                    // Column J or "Sold Price"
                    SOLD_PRICE = GetDecimalValue(row, map, "Sold Price", "SOLD_PRICE", "SoldPrice", "J"),

                    // Column K or "Status"
                    STATUS = GetValue(row, map, "Status", "STATUS", "K"),

                    // Column L or "REPO #" - with # symbol
                    REPO = GetValue(row, map, "REPO #", "REPO#", "Repo #", "REPO", "L"),

                    // Column M or "Location"
                    LOCATION = GetValue(row, map, "Location", "LOCATION", "M"),

                    // Column N or "Date Sold"
                    DATE_SOLD = GetDateValue(row, map, "Date Sold", "DATE_SOLD", "DateSold", "N")
                };

                if (!string.IsNullOrWhiteSpace(item.SKU))
                {
                    InventoryRepository.InsertItem(item);
                }

                processedRows++;

                // Log progress every 100 rows for large files
                if (processedRows % 100 == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Processed {processedRows}/{totalRows} rows...");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Import completed. Processed {processedRows} rows.");

            // Show success message on UI thread
            Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show($"Import completed successfully!\nImported {processedRows} items.",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        // Helper method to get string value with multiple possible column names
        private string GetValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (map.TryGetValue(name, out var index) && index < row.ItemArray.Length)
                {
                    var value = row[index];
                    if (value != null && value != DBNull.Value)
                        return value.ToString().Trim();
                }
            }
            return string.Empty;
        }

        // Helper method to get int value
        private int GetIntValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            var value = GetValue(row, map, possibleNames);
            return int.TryParse(value, out var result) ? result : 0;
        }

        // Helper method to get decimal value
        private decimal GetDecimalValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            var value = GetValue(row, map, possibleNames);
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Remove currency symbols, commas, and spaces
            value = value.Replace("$", "").Replace(",", "").Replace(" ", "").Trim();

            return decimal.TryParse(value, out var result) ? result : 0m;
        }

        // Helper method to get date value without time
        private string GetDateValue(DataRow row, Dictionary<string, int> map, params string[] possibleNames)
        {
            var value = GetValue(row, map, possibleNames);
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Try to parse as date and return only date portion
            if (DateTime.TryParse(value, out var date))
                return date.ToString("M/d/yyyy");

            return value;
        }

        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(txtSearch.Text);
        }

        private void ApplyFilter(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) inventoryDataGrid.ItemsSource = _items;
            else inventoryDataGrid.ItemsSource = new ObservableCollection<InventoryItem>(_items.Where(i =>
                (i.SKU?.Contains(txt, StringComparison.OrdinalIgnoreCase) == true) ||
                (i.DESCRIPTION?.Contains(txt, StringComparison.OrdinalIgnoreCase) == true)));
        }

        // ** Coloring logic **
        private void InventoryDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.Item is InventoryItem item)) return;
            var status = item.STATUS?.ToLower() ?? string.Empty;
            var location = item.LOCATION?.ToLower() ?? string.Empty;
            System.Windows.Media.Brush brush = System.Windows.Media.Brushes.LightGray;
            if (status.Contains("damaged") || status.Contains("missing") || status.Contains("broken") ||
                location.Contains("damaged") || location.Contains("missing") || location.Contains("broken"))
            {
                brush = System.Windows.Media.Brushes.DarkGray;
            }
            else if (status.Contains("sold"))
            {
                switch (location)
                {
                    case "repo": brush = System.Windows.Media.Brushes.LightBlue; break;
                    case "ebay": brush = System.Windows.Media.Brushes.LightGreen; break;
                    case "facebook": brush = System.Windows.Media.Brushes.LightCoral; break;
                    case "business": brush = System.Windows.Media.Brushes.LightGoldenrodYellow; break;
                    case "donated": brush = System.Windows.Media.Brushes.LightPink; break;
                    default: brush = System.Windows.Media.Brushes.LightGray; break;
                }
            }
            else if (status == "repo") brush = System.Windows.Media.Brushes.LightBlue;
            else if (status == "ebay") brush = System.Windows.Media.Brushes.LightGreen;
            else if (status == "facebook") brush = System.Windows.Media.Brushes.LightCoral;
            else if (status == "business") brush = System.Windows.Media.Brushes.LightGoldenrodYellow;
            else if (status == "donated") brush = System.Windows.Media.Brushes.LightPink;

            e.Row.Background = brush;
        }

        private static string EscapeForCsv(string s)
            => string.IsNullOrEmpty(s) ? string.Empty : $"\"{s.Replace("\"", "\"\"")}\"";
    }

    public static class DataRowExtensions
    {
        public static string SafeGet(this DataRow row, System.Collections.Generic.Dictionary<string, int> map, string key)
            => map.TryGetValue(key.ToLower(), out var i) && row[i] != DBNull.Value ? row[i].ToString() : string.Empty;

        public static int SafeGetInt(this DataRow row, System.Collections.Generic.Dictionary<string, int> map, string key)
            => int.TryParse(row.SafeGet(map, key), out var v) ? v : 0;

        public static decimal SafeGetDecimal(this DataRow row, System.Collections.Generic.Dictionary<string, int> map, string key)
        {
            var value = row.SafeGet(map, key);
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Remove currency symbols and commas
            value = value.Replace("$", "").Replace(",", "").Trim();

            return decimal.TryParse(value, out var v) ? v : 0m;
        }
    }
}
