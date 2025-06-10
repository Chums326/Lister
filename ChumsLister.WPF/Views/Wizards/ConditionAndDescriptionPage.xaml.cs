using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Interaction logic for ConditionAndDescriptionPage.xaml
    /// </summary>
    public partial class ConditionAndDescriptionPage : Page, IWizardPage
    {
        private readonly IProductScraper _productScraper;
        private readonly IAIService _aiService;
        private readonly IEbayService _ebayService;
        private string _accountId;
        private string _categoryId;
        private ObservableCollection<ConditionType> _conditions;

        public ConditionAndDescriptionPage(IProductScraper productScraper, IAIService aiService, IEbayService ebayService)
        {
            InitializeComponent();
            _productScraper = productScraper;
            _aiService = aiService;
            _ebayService = ebayService;

            _conditions = new ObservableCollection<ConditionType>();
            cboCondition.ItemsSource = _conditions;

            // Initialize description editor
            txtDescription.TextChanged += TxtDescription_TextChanged;
            UpdateConditionCharCount();
        }

        public bool ValidatePage()
        {
            if (cboCondition.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Please select an item condition", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtDescription.Text))
            {
                System.Windows.MessageBox.Show("Please enter an item description", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (txtDescription.Text.Length < 10)
            {
                System.Windows.MessageBox.Show("Description must be at least 10 characters long", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            var condition = cboCondition.SelectedItem as ConditionType;
            if (condition != null)
            {
                listingData.ConditionId = condition.ConditionId;
                listingData.ConditionName = condition.ConditionDisplayName;
            }

            listingData.ConditionDescription = txtConditionDescription.Text.Trim();
            listingData.Description = txtDescription.Text.Trim();

            var template = cboTemplate.SelectedItem as ComboBoxItem;
            listingData.DescriptionTemplate = template?.Content?.ToString();
        }

        public async void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;
            _categoryId = listingData.PrimaryCategoryId;

            // Load conditions for the category
            if (!string.IsNullOrEmpty(_accountId) && !string.IsNullOrEmpty(_categoryId))
            {
                await LoadCategoryConditions();
            }

            // Restore selections
            if (!string.IsNullOrEmpty(listingData.ConditionId))
            {
                var condition = _conditions.FirstOrDefault(c => c.ConditionId == listingData.ConditionId);
                if (condition != null)
                {
                    cboCondition.SelectedItem = condition;
                }
            }

            txtConditionDescription.Text = listingData.ConditionDescription ?? "";
            txtDescription.Text = listingData.Description ?? "";

            if (!string.IsNullOrEmpty(listingData.DescriptionTemplate))
            {
                foreach (ComboBoxItem item in cboTemplate.Items)
                {
                    if (item.Content.ToString() == listingData.DescriptionTemplate)
                    {
                        cboTemplate.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private async Task LoadCategoryConditions()
        {
            try
            {
                var conditions = await _ebayService.GetCategoryConditionsAsync(_accountId, _categoryId);
                _conditions.Clear();

                foreach (var condition in conditions)
                {
                    _conditions.Add(condition);
                }

                // Select "New" by default if available
                var newCondition = _conditions.FirstOrDefault(c => c.ConditionId == "1000");
                if (newCondition != null)
                {
                    cboCondition.SelectedItem = newCondition;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading conditions: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void cboCondition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cboCondition.SelectedItem is ConditionType condition)
            {
                // Update condition guidelines based on selection
                switch (condition.ConditionId)
                {
                    case "1000": // New
                        txtConditionGuidelines.Text = "• Brand new, unused, unopened, undamaged item\n" +
                                                     "• In original packaging (where packaging is applicable)\n" +
                                                     "• May include original tags";
                        break;

                    case "1500": // New other
                        txtConditionGuidelines.Text = "• New, unused item with no signs of wear\n" +
                                                     "• May be missing original packaging or tags\n" +
                                                     "• May have been stored improperly";
                        break;

                    case "2000": // Used
                        txtConditionGuidelines.Text = "• Previously used item\n" +
                                                     "• May show some signs of cosmetic wear\n" +
                                                     "• Fully operational and functions as intended\n" +
                                                     "• Describe any flaws or defects";
                        break;

                    case "2500": // Seller refurbished
                        txtConditionGuidelines.Text = "• Restored to working order by seller\n" +
                                                     "• Cleaned, inspected, and repaired if necessary\n" +
                                                     "• May have cosmetic imperfections\n" +
                                                     "• Include details of refurbishment";
                        break;

                    case "3000": // For parts or not working
                        txtConditionGuidelines.Text = "• Does not function properly or cannot be used\n" +
                                                     "• May be defective or missing parts\n" +
                                                     "• Sold for parts or repair only\n" +
                                                     "• Clearly describe all issues";
                        break;

                    default:
                        txtConditionGuidelines.Text = "Select a condition to see guidelines";
                        break;
                }
            }
        }

        private void TxtDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Update preview
            UpdatePreview();
        }

        private void TxtConditionDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateConditionCharCount();
        }

        private void UpdateConditionCharCount()
        {
            int charCount = txtConditionDescription.Text?.Length ?? 0;
            txtConditionCharCount.Text = $"{charCount}/1000 characters";
        }

        private void UpdatePreview()
        {
            var template = cboTemplate.SelectedItem as ComboBoxItem;
            string templateName = template?.Content?.ToString() ?? "No Template";

            StringBuilder html = new StringBuilder();
            html.AppendLine("<html><head>");
            html.AppendLine("<style>");

            // Add template-specific styles
            switch (templateName)
            {
                case "Professional":
                    html.AppendLine("body { font-family: Arial, sans-serif; color: #333; line-height: 1.6; }");
                    html.AppendLine("h2 { color: #0654ba; border-bottom: 2px solid #0654ba; padding-bottom: 5px; }");
                    html.AppendLine(".container { max-width: 800px; margin: 0 auto; padding: 20px; }");
                    break;

                case "Simple":
                    html.AppendLine("body { font-family: Verdana, sans-serif; color: #000; line-height: 1.5; }");
                    html.AppendLine("h2 { color: #000; font-size: 18px; margin-top: 20px; }");
                    html.AppendLine(".container { padding: 10px; }");
                    break;

                case "Mobile Friendly":
                    html.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }");
                    html.AppendLine("h2 { font-size: 1.2em; margin: 15px 0; }");
                    html.AppendLine(".container { padding: 15px; font-size: 16px; }");
                    html.AppendLine("@media (max-width: 600px) { body { font-size: 14px; } }");
                    break;

                default:
                    html.AppendLine("body { font-family: Arial, sans-serif; }");
                    break;
            }

            html.AppendLine("</style></head><body>");
            html.AppendLine("<div class='container'>");

            // Convert text to HTML
            string descriptionHtml = System.Web.HttpUtility.HtmlEncode(txtDescription.Text);
            descriptionHtml = descriptionHtml.Replace("\n", "<br/>");

            html.AppendLine(descriptionHtml);
            html.AppendLine("</div></body></html>");

            // Navigate to the HTML
            webPreview.NavigateToString(html.ToString());
        }

        private async void btnEnhanceDescription_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDescription.Text))
            {
                System.Windows.MessageBox.Show("Please enter a description first", "No Description",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                var wizard = Window.GetWindow(this) as WizardWindow;
                var wizardData = wizard?.WizardData;

                if (wizardData == null)
                {
                    System.Windows.MessageBox.Show("Could not access wizard data", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create product data
                var product = new ProductData
                {
                    Title = wizardData.Title,
                    BrandModel = wizardData.Brand,
                    ModelNumber = wizardData.MPN,
                    Description = txtDescription.Text,
                    ItemType = wizardData.PrimaryCategoryName,
                    Condition = wizardData.ConditionName
                };

                // Add item specifics
                foreach (var kvp in wizardData.ItemSpecifics)
                {
                    product.ItemSpecifics[kvp.Key] = kvp.Value.FirstOrDefault();
                }

                // Call AI service
                string enhancedDescription = await _aiService.EnhanceDescriptionAsync(product);

                if (!string.IsNullOrEmpty(enhancedDescription))
                {
                    string originalDescription = txtDescription.Text;
                    txtDescription.Text = enhancedDescription;

                    var result = System.Windows.MessageBox.Show(
                        "AI has enhanced your description. Would you like to keep it?",
                        "AI Enhancement",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        txtDescription.Text = originalDescription;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error enhancing description: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Text formatting buttons
        private void btnBold_Click(object sender, RoutedEventArgs e)
        {
            InsertFormatting("**", "**");
        }

        private void btnItalic_Click(object sender, RoutedEventArgs e)
        {
            InsertFormatting("*", "*");
        }

        private void btnUnderline_Click(object sender, RoutedEventArgs e)
        {
            InsertFormatting("<u>", "</u>");
        }

        private void btnBulletList_Click(object sender, RoutedEventArgs e)
        {
            InsertFormatting("\n• ", "\n");
        }

        private void btnNumberList_Click(object sender, RoutedEventArgs e)
        {
            InsertFormatting("\n1. ", "\n");
        }

        private void InsertFormatting(string startTag, string endTag)
        {
            int selectionStart = txtDescription.SelectionStart;
            int selectionLength = txtDescription.SelectionLength;

            if (selectionLength > 0)
            {
                string selectedText = txtDescription.Text.Substring(selectionStart, selectionLength);
                string formattedText = startTag + selectedText + endTag;

                txtDescription.Text = txtDescription.Text.Remove(selectionStart, selectionLength);
                txtDescription.Text = txtDescription.Text.Insert(selectionStart, formattedText);

                txtDescription.SelectionStart = selectionStart + formattedText.Length;
            }
            else
            {
                txtDescription.Text = txtDescription.Text.Insert(selectionStart, startTag + "text" + endTag);
                txtDescription.SelectionStart = selectionStart + startTag.Length;
                txtDescription.SelectionLength = 4; // Select "text"
            }

            txtDescription.Focus();
        }
    }
}