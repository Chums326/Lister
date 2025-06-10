using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;
using ChumsLister.WPF.Converters;
using Microsoft.Win32;
using System.Net.Http;
using System.Diagnostics;
using System.Collections.Generic;

namespace ChumsLister.WPF.Views.Wizards
{
    public partial class ImagesPage : Page, IWizardPage
    {
        private readonly IProductScraper _productScraper;
        private readonly IAIService _aiService;
        private readonly ObservableCollection<ImageItem> _images;
        private readonly HttpClient _httpClient;

        public ImagesPage(IProductScraper productScraper, IAIService aiService)
        {
            InitializeComponent();
            _productScraper = productScraper;
            _aiService = aiService;
            _images = new ObservableCollection<ImageItem>();
            _httpClient = new HttpClient();

            // Set up the ListBox data binding
            listImages.ItemsSource = _images;
        }

        public bool ValidatePage()
        {
            // At least one image is recommended for eBay listings
            if (_images.Count == 0)
            {
                var result = System.Windows.MessageBox.Show(
                    "No images have been added to your listing. eBay listings perform much better with images.\n\nWould you like to continue without images?",
                    "No Images Added",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                return result == MessageBoxResult.Yes;
            }

            // Check if any images failed to load
            var failedImages = _images.Where(img => img.Status == ImageStatus.Error).ToList();
            if (failedImages.Any())
            {
                var result = System.Windows.MessageBox.Show(
                    $"{failedImages.Count} image(s) failed to load. Would you like to continue anyway?\n\nFailed images will be excluded from the listing.",
                    "Image Load Errors",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                return result == MessageBoxResult.Yes;
            }

            return true;
        }

        public void LoadData(ListingWizardData listingData)
        {
            _images.Clear();

            // Load existing image URLs from wizard data
            if (listingData.ImageUrls != null && listingData.ImageUrls.Count > 0)
            {
                foreach (var imageUrl in listingData.ImageUrls)
                {
                    var imageItem = new ImageItem
                    {
                        SourcePath = imageUrl,
                        IsUrl = Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute),
                        Status = ImageStatus.Loading
                    };

                    _images.Add(imageItem);

                    // Load image asynchronously
                    _ = LoadImageAsync(imageItem);
                }
            }

            UpdateImageCountLabel();
        }

        public void SaveData(ListingWizardData listingData)
        {
            listingData.ImageUrls.Clear();

            // Save only successfully loaded images
            foreach (var imageItem in _images.Where(img => img.Status == ImageStatus.Loaded))
            {
                listingData.ImageUrls.Add(imageItem.FinalUrl ?? imageItem.SourcePath);
            }

            // Also store local paths for later upload to eBay
            if (listingData.LocalImagePaths == null)
                listingData.LocalImagePaths = new List<string>();

            listingData.LocalImagePaths.Clear();
            listingData.LocalImagePaths.AddRange(_images
                .Where(img => !img.IsUrl && img.Status == ImageStatus.Loaded)
                .Select(img => img.SourcePath));
        }

        private async void BtnAddLocalImage_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Images",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff|" +
                        "JPEG Files|*.jpg;*.jpeg|" +
                        "PNG Files|*.png|" +
                        "All Files|*.*",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filePath in openFileDialog.FileNames)
                {
                    await AddLocalImageAsync(filePath);
                }
            }
        }

        private async void BtnAddUrlImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ImageUrlDialog();
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ImageUrl))
            {
                await AddUrlImageAsync(dialog.ImageUrl);
            }
        }

        private async Task AddLocalImageAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Windows.MessageBox.Show($"File not found: {filePath}", "File Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Check file size (eBay has limits)
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > 12 * 1024 * 1024) // 12MB limit
                {
                    System.Windows.MessageBox.Show($"Image file is too large: {fileInfo.Length / (1024 * 1024)}MB. Maximum size is 12MB.",
                                  "File Too Large", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var imageItem = new ImageItem
                {
                    SourcePath = filePath,
                    IsUrl = false,
                    Status = ImageStatus.Loading,
                    FileName = Path.GetFileName(filePath)
                };

                _images.Add(imageItem);
                UpdateImageCountLabel();

                await LoadImageAsync(imageItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding image: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddUrlImageAsync(string imageUrl)
        {
            try
            {
                if (!Uri.IsWellFormedUriString(imageUrl, UriKind.Absolute))
                {
                    System.Windows.MessageBox.Show("Please enter a valid image URL.", "Invalid URL",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var imageItem = new ImageItem
                {
                    SourcePath = imageUrl,
                    IsUrl = true,
                    Status = ImageStatus.Loading,
                    FileName = Path.GetFileName(new Uri(imageUrl).LocalPath)
                };

                _images.Add(imageItem);
                UpdateImageCountLabel();

                await LoadImageAsync(imageItem);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding image URL: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadImageAsync(ImageItem imageItem)
        {
            try
            {
                BitmapImage bitmap = null;

                if (imageItem.IsUrl)
                {
                    // Load from URL
                    var response = await _httpClient.GetAsync(imageItem.SourcePath);
                    if (response.IsSuccessStatusCode)
                    {
                        var imageData = await response.Content.ReadAsByteArrayAsync();
                        bitmap = CreateBitmapFromBytes(imageData);
                        imageItem.FinalUrl = imageItem.SourcePath;
                    }
                    else
                    {
                        throw new Exception($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
                    }
                }
                else
                {
                    // Load from local file
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imageItem.SourcePath, UriKind.Absolute);
                    bitmap.DecodePixelWidth = 200; // Thumbnail size
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                imageItem.Thumbnail = bitmap;
                imageItem.Status = ImageStatus.Loaded;
                imageItem.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading image {imageItem.SourcePath}: {ex.Message}");
                imageItem.Status = ImageStatus.Error;
                imageItem.ErrorMessage = ex.Message;
                imageItem.Thumbnail = null;
            }
        }

        private BitmapImage CreateBitmapFromBytes(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageData);
            bitmap.DecodePixelWidth = 200; // Thumbnail size
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Forms.Button button && button.DataContext is ImageItem imageItem)
            {
                _images.Remove(imageItem);
                UpdateImageCountLabel();
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Forms.Button button && button.DataContext is ImageItem imageItem)
            {
                var index = _images.IndexOf(imageItem);
                if (index > 0)
                {
                    _images.Move(index, index - 1);
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Forms.Button button && button.DataContext is ImageItem imageItem)
            {
                var index = _images.IndexOf(imageItem);
                if (index < _images.Count - 1)
                {
                    _images.Move(index, index + 1);
                }
            }
        }

        private void UpdateImageCountLabel()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (lblImageCount != null)
                {
                    lblImageCount.Content = $"Images: {_images.Count}/12";

                    if (_images.Count >= 12)
                    {
                        btnAddLocalImage.IsEnabled = false;
                        btnAddUrlImage.IsEnabled = false;
                    }
                    else
                    {
                        btnAddLocalImage.IsEnabled = true;
                        btnAddUrlImage.IsEnabled = true;
                    }
                }
            });
        }

        private async void BtnOptimizeImages_Click(object sender, RoutedEventArgs e)
        {
            if (_aiService == null)
            {
                System.Windows.MessageBox.Show("AI service is not available.", "Feature Unavailable",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

                // Get wizard data to provide context to AI
                var wizard = Window.GetWindow(this) as WizardWindow;
                var wizardData = wizard?.WizardData;

                if (wizardData == null)
                {
                    System.Windows.MessageBox.Show("Could not access listing data for optimization.", "Error",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create product data for AI analysis
                var product = new ProductData
                {
                    Title = wizardData.Title,
                    Description = wizardData.Description,
                    BrandModel = wizardData.Brand,
                    ItemType = wizardData.PrimaryCategoryName,
                    ImagePaths = _images.Where(img => img.Status == ImageStatus.Loaded)
                                       .Select(img => img.SourcePath).ToList()
                };

                // Use AI to suggest image improvements or reordering
                var suggestions = await _aiService.OptimizeImagesAsync(product);

                if (!string.IsNullOrEmpty(suggestions))
                {
                    var result = System.Windows.MessageBox.Show(
                        $"AI Image Suggestions:\n\n{suggestions}\n\nWould you like to apply these suggestions?",
                        "AI Image Optimization",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    // For now, just show suggestions. You could implement auto-reordering here
                    if (result == MessageBoxResult.Yes)
                    {
                        System.Windows.MessageBox.Show("Image optimization suggestions noted. Manual reordering can be done using the up/down buttons.",
                                      "Suggestions Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error optimizing images: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }
    }

    // Supporting classes
    public class ImageItem
    {
        public string SourcePath { get; set; }
        public string FinalUrl { get; set; }
        public bool IsUrl { get; set; }
        public string FileName { get; set; }
        public BitmapImage Thumbnail { get; set; }
        public ImageStatus Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    public enum ImageStatus
    {
        Loading,
        Loaded,
        Error
    }
}