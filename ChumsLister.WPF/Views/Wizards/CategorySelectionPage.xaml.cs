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
    public partial class CategorySelectionPage : Page, IWizardPage
    {
        private readonly IEbayService _ebayService;
        private readonly ObservableCollection<CategoryNode> _categories;
        private readonly ObservableCollection<CategoryNode> _secondaryCategories;
        private readonly ObservableCollection<StoreCategoryViewModel> _storeCategories;
        private string _accountId;

        public CategorySelectionPage(IEbayService ebayService)
        {
            InitializeComponent();
            _ebayService = ebayService;

            _categories = new ObservableCollection<CategoryNode>();
            _secondaryCategories = new ObservableCollection<CategoryNode>();
            _storeCategories = new ObservableCollection<StoreCategoryViewModel>();

            treeCategories.ItemsSource = _categories;
            treeSecondaryCategories.ItemsSource = _secondaryCategories;
            listStoreCategories.ItemsSource = _storeCategories;

            // Hook TreeViewItem.Expanded for lazy loading
            treeCategories.AddHandler(
                TreeViewItem.ExpandedEvent,
                new RoutedEventHandler(CategoryTree_Expanded));
            treeSecondaryCategories.AddHandler(
                TreeViewItem.ExpandedEvent,
                new RoutedEventHandler(CategoryTree_Expanded));
        }

        public bool ValidatePage()
        {
            // Check if a valid category is selected
            var selectedNode = treeCategories.SelectedItem as CategoryNode;

            if (selectedNode == null || string.IsNullOrEmpty(selectedNode.CategoryId))
            {
                System.Windows.MessageBox.Show(
                    "Please select a primary category",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Check the current state of IsLeaf (which may have been updated after expansion)
            if (!selectedNode.IsLeaf)
            {
                // If it has the "Loading..." placeholder, it hasn't been expanded yet
                if (selectedNode.Children?.Count == 1 && selectedNode.Children[0].CategoryName == "Loading...")
                {
                    System.Windows.MessageBox.Show(
                        "Please expand this category to see if it has subcategories before selecting it.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
                // If it has actual children, it's not a leaf
                else if (selectedNode.Children?.Count > 0)
                {
                    System.Windows.MessageBox.Show(
                        "Please select a leaf category (one with no subcategories). The selected category has subcategories.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        public void SaveData(ListingWizardData listingData)
        {
            // Save primary category
            if (treeCategories.SelectedItem is CategoryNode selectedPrimary)
            {
                listingData.PrimaryCategoryId = selectedPrimary.CategoryId;
                listingData.PrimaryCategoryName = selectedPrimary.CategoryName;
            }

            // Save secondary category (if checked)
            if (chkSecondaryCategory.IsChecked == true &&
                treeSecondaryCategories.SelectedItem is CategoryNode selectedSecondary)
            {
                listingData.SecondaryCategoryId = selectedSecondary.CategoryId;
                listingData.SecondaryCategoryName = selectedSecondary.CategoryName;
            }

            // Save up to 2 store categories
            listingData.StoreCategoryIds.Clear();
            listingData.StoreCategoryNames.Clear();
            foreach (var sc in _storeCategories.Where(c => c.IsSelected).Take(2))
            {
                listingData.StoreCategoryIds.Add(sc.CategoryId);
                listingData.StoreCategoryNames.Add(sc.CategoryName);
            }
        }

        public async void LoadData(ListingWizardData listingData)
        {
            _accountId = listingData.SelectedAccountId;

            if (!string.IsNullOrEmpty(_accountId))
            {
                // Load root categories
                await LoadCategories();
                // Load store categories
                await LoadStoreCategories();
            }

            // If editing an existing listing, restore previous selection:
            if (!string.IsNullOrEmpty(listingData.PrimaryCategoryId))
            {
                txtSelectedPrimary.Text =
                    $"{listingData.PrimaryCategoryName} ({listingData.PrimaryCategoryId})";
                // Optionally you could expand the tree to that node here
            }

            if (!string.IsNullOrEmpty(listingData.SecondaryCategoryId))
            {
                chkSecondaryCategory.IsChecked = true;
                // Optionally expand secondary tree
            }
        }

        private async Task LoadCategories()
        {
            try
            {
                var rootCats = await _ebayService.GetCategoriesAsync(_accountId, parentId: null);
                _categories.Clear();

                foreach (var cat in rootCats)
                {
                    System.Diagnostics.Debug.WriteLine($"Loading root category: {cat.CategoryName} ({cat.CategoryId}), LeafCategory: {cat.LeafCategory}");

                    var node = new CategoryNode
                    {
                        CategoryId = cat.CategoryId,
                        CategoryName = cat.CategoryName,
                        IsLeaf = cat.LeafCategory,
                        Children = new ObservableCollection<CategoryNode>()
                    };

                    if (!cat.LeafCategory)
                    {
                        // Add a single placeholder child for lazy loading
                        node.Children.Add(new CategoryNode
                        {
                            CategoryName = "Loading...",
                            CategoryId = null,
                            IsLeaf = false,
                            Children = null
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  ^ Marked as leaf, no expansion placeholder added");
                    }

                    _categories.Add(node);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading categories: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task LoadStoreCategories()
        {
            try
            {
                var storeCats = await _ebayService.GetStoreCategoriesAsync(_accountId);
                _storeCategories.Clear();

                foreach (var cat in storeCats)
                {
                    _storeCategories.Add(new StoreCategoryViewModel
                    {
                        CategoryId = cat.CategoryId,
                        CategoryName = cat.CategoryName,
                        IsSelected = false
                    });
                }
            }
            catch (Exception ex)
            {
                // Store categories are optional; log any error and continue
                System.Diagnostics.Debug.WriteLine($"Error loading store categories: {ex.Message}");
            }
        }

        /// <summary>
        /// Lazy load children when a TreeViewItem is expanded.
        /// </summary>
        // Replace the CategoryTree_Expanded method in CategorySelectionPage.xaml.cs:

        // Replace the CategoryTree_Expanded method in CategorySelectionPage.xaml.cs:

        private async void CategoryTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (!(e.OriginalSource is TreeViewItem item)) return;
            if (!(item.DataContext is CategoryNode node)) return;

            // If we have exactly one placeholder child "Loading...", replace it
            if (!node.IsLeaf
                && node.Children?.Count == 1
                && node.Children[0].CategoryName == "Loading...")
            {
                node.Children.Clear();

                try
                {
                    System.Diagnostics.Debug.WriteLine($"Loading subcategories for: {node.CategoryName} ({node.CategoryId})");

                    var subcats = await _ebayService.GetCategoriesAsync(_accountId, parentId: node.CategoryId);

                    System.Diagnostics.Debug.WriteLine($"API returned {subcats.Count} subcategories for {node.CategoryId}");

                    if (subcats.Count == 0)
                    {
                        // Don't set IsLeaf = true, just leave Children empty
                        // This will make the expansion arrow disappear naturally
                        System.Diagnostics.Debug.WriteLine($"Category {node.CategoryName} ({node.CategoryId}) has no children.");
                    }
                    else
                    {
                        foreach (var cat in subcats)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - Adding child: {cat.CategoryName} ({cat.CategoryId}), IsLeaf: {cat.LeafCategory}");

                            var childNode = new CategoryNode
                            {
                                CategoryId = cat.CategoryId,
                                CategoryName = cat.CategoryName,
                                IsLeaf = cat.LeafCategory,
                                Children = new ObservableCollection<CategoryNode>()
                            };

                            if (!cat.LeafCategory)
                            {
                                childNode.Children.Add(new CategoryNode
                                {
                                    CategoryName = "Loading...",
                                    CategoryId = null,
                                    IsLeaf = false,
                                    Children = null
                                });
                            }
                            node.Children.Add(childNode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR loading subcategories for {node.CategoryId}: {ex}");
                    System.Windows.MessageBox.Show(
                        $"Error loading subcategories: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        // Also update the TreeCategories_SelectedItemChanged to show better feedback:
        private async void TreeCategories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeCategories.SelectedItem is CategoryNode selected)
            {
                // Update the selected category display
                txtSelectedPrimary.Text = $"{selected.CategoryName} ({selected.CategoryId})";

                // Show if this is a leaf category
                if (selected.IsLeaf)
                {
                    txtSelectedPrimary.Text += " ✓ [Selectable]";
                    txtSelectedPrimary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else if (selected.Children?.Count == 1 && selected.Children[0].CategoryName == "Loading...")
                {
                    txtSelectedPrimary.Text += " [Click arrow to expand]";
                    txtSelectedPrimary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                }
                else
                {
                    txtSelectedPrimary.Text += " [Has subcategories]";
                    txtSelectedPrimary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }

                // Load and display category specifics preview
                try
                {
                    if (selected.IsLeaf)
                    {
                        var specs = await _ebayService.GetCategorySpecificsAsync(_accountId, selected.CategoryId);
                        if (specs?.Count > 0)
                        {
                            var requiredSpecs = specs.Where(s => s.Required).Take(5).Select(s => s.Name);
                            var optionalSpecs = specs.Where(s => !s.Required).Take(3).Select(s => s.Name);

                            var featuresText = "";
                            if (requiredSpecs.Any())
                            {
                                featuresText = "Required: " + string.Join(", ", requiredSpecs);
                            }
                            if (optionalSpecs.Any())
                            {
                                if (!string.IsNullOrEmpty(featuresText)) featuresText += "\n";
                                featuresText += "Optional: " + string.Join(", ", optionalSpecs);
                            }

                            txtCategoryFeatures.Text = featuresText;
                        }
                        else
                        {
                            txtCategoryFeatures.Text = "No specific features for this category.";
                        }
                    }
                    else
                    {
                        txtCategoryFeatures.Text = "Expand this category to see available subcategories.";
                    }
                }
                catch (Exception ex)
                {
                    txtCategoryFeatures.Text = $"Unable to load category features: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Error loading category specifics: {ex}");
                }
            }
        }

        /// <summary>
        /// Gets fired when the user clicks "Suggest".
        /// </summary>
        private async void btnSuggestCategory_Click(object sender, RoutedEventArgs e)
        {
            // Check if we have a title from the listing data
            var window = Window.GetWindow(this);
            if (window?.DataContext is ListingWizardData wizardData && !string.IsNullOrWhiteSpace(wizardData.Title))
            {
                try
                {
                    var suggestion = await _ebayService.SuggestCategoryAsync(_accountId, wizardData.Title);
                    if (suggestion != null && !string.IsNullOrEmpty(suggestion.CategoryId))
                    {
                        System.Windows.MessageBox.Show(
                            $"Suggested Category:\n\n{suggestion.CategoryName}\nCategory ID: {suggestion.CategoryId}\nConfidence: {suggestion.PercentFound:F1}%\n\nPlease navigate to this category in the tree.",
                            "Category Suggestion",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // You could also implement auto-navigation to the suggested category here
                    }
                    else
                    {
                        System.Windows.MessageBox.Show(
                            "No category suggestions found for this title.",
                            "Category Suggestion",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Error getting category suggestion: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Please enter a title on the Title & SKU page first, then click Suggest to get category recommendations.",
                    "Title Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void TreeSecondaryCategories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (treeSecondaryCategories.SelectedItem is CategoryNode selected)
            {
                // You could show secondary category info in a separate TextBlock if needed
                System.Diagnostics.Debug.WriteLine($"Secondary category selected: {selected.CategoryName} ({selected.CategoryId})");
            }
        }

        private void chkSecondaryCategory_Checked(object sender, RoutedEventArgs e)
        {
            treeSecondaryCategories.Visibility = Visibility.Visible;

            // Copy the root categories into the secondary tree (lazy placeholders)
            if (_secondaryCategories.Count == 0)
            {
                foreach (var root in _categories)
                {
                    var copy = new CategoryNode
                    {
                        CategoryId = root.CategoryId,
                        CategoryName = root.CategoryName,
                        IsLeaf = root.IsLeaf,
                        Children = new ObservableCollection<CategoryNode>()
                    };
                    if (!root.IsLeaf)
                    {
                        copy.Children.Add(new CategoryNode
                        {
                            CategoryName = "Loading...",
                            CategoryId = null,
                            IsLeaf = false,
                            Children = null
                        });
                    }
                    _secondaryCategories.Add(copy);
                }
            }
        }

        private void chkSecondaryCategory_Unchecked(object sender, RoutedEventArgs e)
        {
            treeSecondaryCategories.Visibility = Visibility.Collapsed;
            _secondaryCategories.Clear();
        }
    }
}