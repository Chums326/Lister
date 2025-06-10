using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class ItemSpecificsPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;
        private ObservableCollection<SpecificViewModel> _requiredSpecifics;
        private ObservableCollection<SpecificViewModel> _additionalSpecifics;
        private string _accountId;
        private string _categoryId;

        public ItemSpecificsPage(IEbayService ebayService)
        {
            InitializeComponent();
            _ebayService = ebayService;

            _requiredSpecifics = new ObservableCollection<SpecificViewModel>();
            _additionalSpecifics = new ObservableCollection<SpecificViewModel>();

            requiredSpecifics.ItemsSource = _requiredSpecifics;
            gridAdditionalSpecifics.ItemsSource = _additionalSpecifics;
        }

        public bool ValidatePage()
        {
            // Check all required specifics have values
            foreach (var specific in _requiredSpecifics)
            {
                if (string.IsNullOrWhiteSpace(specific.Value) &&
                    string.IsNullOrWhiteSpace(specific.SelectedValue))
                {
                    System.Windows.MessageBox.Show($"Please provide a value for required field: {specific.Name}",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            listingData.ItemSpecifics.Clear();

            // Save required specifics
            foreach (var specific in _requiredSpecifics)
            {
                var value = !string.IsNullOrEmpty(specific.SelectedValue)
                    ? specific.SelectedValue
                    : specific.Value;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    listingData.ItemSpecifics[specific.Name] = new List<string> { value };
                }
            }

            // Save additional specifics
            foreach (var specific in _additionalSpecifics)
            {
                if (!string.IsNullOrWhiteSpace(specific.Value))
                {
                    listingData.ItemSpecifics[specific.Name] = new List<string> { specific.Value };
                }
            }
        }

        public async void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;
            _categoryId = listingData.PrimaryCategoryId;  // This is the key fix!

            if (!string.IsNullOrEmpty(_accountId) && !string.IsNullOrEmpty(_categoryId))
            {
                await LoadCategorySpecifics();

                // Restore any existing values
                foreach (var kvp in listingData.ItemSpecifics)
                {
                    var required = _requiredSpecifics.FirstOrDefault(s => s.Name == kvp.Key);
                    if (required != null)
                    {
                        if (required.SelectionMode == "SelectionOnly")
                            required.SelectedValue = kvp.Value.FirstOrDefault();
                        else
                            required.Value = kvp.Value.FirstOrDefault();
                    }
                    else
                    {
                        // Add to additional specifics if not in required
                        _additionalSpecifics.Add(new SpecificViewModel
                        {
                            Name = kvp.Key,
                            Value = kvp.Value.FirstOrDefault(),
                            SelectionMode = "FreeText"
                        });
                    }
                }
            }
            else
            {
                // Show a message if category hasn't been selected yet
                if (string.IsNullOrEmpty(_categoryId))
                {
                    _requiredSpecifics.Clear();
                    _additionalSpecifics.Clear();

                    // You might want to show a message to the user
                    System.Windows.MessageBox.Show(
                        "Please select a category first to load the required item specifics.",
                        "Category Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
        }

        private async Task LoadCategorySpecifics()
        {
            try
            {
                var specifics = await _ebayService.GetCategorySpecificsAsync(_accountId, _categoryId);
                _requiredSpecifics.Clear();

                foreach (var specific in specifics.Where(s => s.Required))
                {
                    _requiredSpecifics.Add(new SpecificViewModel
                    {
                        Name = specific.Name,
                        Required = specific.Required,
                        SelectionMode = specific.SelectionMode,
                        ValueRecommendations = specific.ValueRecommendations,
                        HelpText = specific.HelpText
                    });
                }

                // Also load recommended (non-required) specifics as suggestions
                var recommendedSpecifics = specifics.Where(s => !s.Required).Take(5); // Show top 5 recommended
                foreach (var specific in recommendedSpecifics)
                {
                    _additionalSpecifics.Add(new SpecificViewModel
                    {
                        Name = specific.Name,
                        Required = false,
                        SelectionMode = specific.SelectionMode,
                        ValueRecommendations = specific.ValueRecommendations,
                        HelpText = specific.HelpText,
                        Value = "" // Empty by default, user can fill if needed
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading category specifics: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddSpecific_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddSpecificDialog();
            if (dialog.ShowDialog() == true)
            {
                // Check if this specific already exists
                var existingRequired = _requiredSpecifics.FirstOrDefault(s =>
                    s.Name.Equals(dialog.SpecificName, StringComparison.OrdinalIgnoreCase));
                var existingAdditional = _additionalSpecifics.FirstOrDefault(s =>
                    s.Name.Equals(dialog.SpecificName, StringComparison.OrdinalIgnoreCase));

                if (existingRequired != null || existingAdditional != null)
                {
                    System.Windows.MessageBox.Show(
                        $"A specific with the name '{dialog.SpecificName}' already exists.",
                        "Duplicate Specific",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _additionalSpecifics.Add(new SpecificViewModel
                {
                    Name = dialog.SpecificName,
                    Value = dialog.SpecificValue,
                    SelectionMode = "FreeText"
                });
            }
        }

        private void btnRemoveSpecific_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.CommandParameter is SpecificViewModel specific)
            {
                _additionalSpecifics.Remove(specific);
            }
        }

        private class SpecificViewModel
        {
            public string Name { get; set; }
            public bool Required { get; set; }
            public string SelectionMode { get; set; }
            public List<string> ValueRecommendations { get; set; }
            public string Value { get; set; }
            public string SelectedValue { get; set; }
            public string HelpText { get; set; }
        }
    }
}