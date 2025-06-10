// File: ChumsLister.WPF/ViewModels/ShippingViewModel.cs

using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Models;                   // brings in MarketplaceOrderSummary, OrderDetail, ShippingRateResult, etc.
using ChumsLister.Core.Services;
using ChumsLister.Core.Services.Marketplaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ChumsLister.WPF.ViewModels
{
    public class ShippingViewModel : ObservableObject
    {
        private readonly IMarketplaceServiceFactory _marketplaceFactory;
        private EbayMarketplaceService _ebayService;

        public ShippingViewModel(IMarketplaceServiceFactory marketplaceFactory)
        {
            _marketplaceFactory = marketplaceFactory;
            PendingOrders = new ObservableCollection<MarketplaceOrderSummary>();

            LoadPendingOrdersCommand = new AsyncRelayCommand(LoadPendingOrdersAsync);
            RefreshRatesCommand = new AsyncRelayCommand(RefreshRatesAsync, () => HasOrder);
            PurchaseLabelCommand = new AsyncRelayCommand(PurchaseLabelAsync, () => CanPurchaseLabel);
            CancelCommand = new RelayCommand(_ => Cancel());

            AvailableRates = new ObservableCollection<ShippingRateResult>();
            InitializeDefaults();
        }

        #region Public Properties

        public ObservableCollection<MarketplaceOrderSummary> PendingOrders { get; }

        private MarketplaceOrderSummary _selectedPendingOrder;
        public MarketplaceOrderSummary SelectedPendingOrder
        {
            get => _selectedPendingOrder;
            set
            {
                if (SetProperty(ref _selectedPendingOrder, value) && value != null)
                    _ = LoadOrderDetailsAsync(value.OrderId);
            }
        }

        // Corrected to use the singular OrderDetail type
        private OrderDetails _currentOrder;
        public OrderDetails CurrentOrder
        {
            get => _currentOrder;
            set
            {
                if (SetProperty(ref _currentOrder, value))
                {
                    OnPropertyChanged(nameof(HasOrder));
                    AvailableRates.Clear();
                    SelectedRate = null;
                    ((AsyncRelayCommand)RefreshRatesCommand).NotifyCanExecuteChanged();
                    ((AsyncRelayCommand)PurchaseLabelCommand).NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>True when an order has been loaded.</summary>
        public bool HasOrder => CurrentOrder != null;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        // "From" address fields
        private string _fromName;
        public string FromName
        {
            get => _fromName;
            set => SetProperty(ref _fromName, value);
        }

        private string _fromAddress1;
        public string FromAddress1
        {
            get => _fromAddress1;
            set => SetProperty(ref _fromAddress1, value);
        }

        private string _fromAddress2;
        public string FromAddress2
        {
            get => _fromAddress2;
            set => SetProperty(ref _fromAddress2, value);
        }

        private string _fromCity;
        public string FromCity
        {
            get => _fromCity;
            set => SetProperty(ref _fromCity, value);
        }

        private string _fromState;
        public string FromState
        {
            get => _fromState;
            set => SetProperty(ref _fromState, value);
        }

        private string _fromZip;
        public string FromZip
        {
            get => _fromZip;
            set => SetProperty(ref _fromZip, value);
        }

        // Package dimensions & weight
        private string _length = "10";
        public string Length
        {
            get => _length;
            set => SetProperty(ref _length, value);
        }

        private string _width = "8";
        public string Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private string _height = "4";
        public string Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        private string _weightLbs = "1";
        public string WeightLbs
        {
            get => _weightLbs;
            set => SetProperty(ref _weightLbs, value);
        }

        private string _weightOz = "0";
        public string WeightOz
        {
            get => _weightOz;
            set => SetProperty(ref _weightOz, value);
        }

        // Carrier / service dropdown
        private string _selectedCarrier = "USPS";
        public string SelectedCarrier
        {
            get => _selectedCarrier;
            set
            {
                if (SetProperty(ref _selectedCarrier, value))
                    UpdateServiceTypes();
            }
        }

        private ObservableCollection<string> _serviceTypes = new();
        public ObservableCollection<string> ServiceTypes
        {
            get => _serviceTypes;
            set => SetProperty(ref _serviceTypes, value);
        }

        private string _selectedServiceType;
        public string SelectedServiceType
        {
            get => _selectedServiceType;
            set => SetProperty(ref _selectedServiceType, value);
        }

        private bool _signatureRequired;
        public bool SignatureRequired
        {
            get => _signatureRequired;
            set => SetProperty(ref _signatureRequired, value);
        }

        private bool _insuranceEnabled;
        public bool InsuranceEnabled
        {
            get => _insuranceEnabled;
            set
            {
                if (SetProperty(ref _insuranceEnabled, value) && value && CurrentOrder != null)
                    InsuranceValue = CurrentOrder.OrderTotal.ToString("F2");
            }
        }

        private string _insuranceValue = "0.00";
        public string InsuranceValue
        {
            get => _insuranceValue;
            set => SetProperty(ref _insuranceValue, value);
        }

        // Available shipping rates
        private ObservableCollection<ShippingRateResult> _availableRates;
        public ObservableCollection<ShippingRateResult> AvailableRates
        {
            get => _availableRates;
            set => SetProperty(ref _availableRates, value);
        }

        private ShippingRateResult _selectedRate;
        public ShippingRateResult SelectedRate
        {
            get => _selectedRate;
            set
            {
                if (SetProperty(ref _selectedRate, value))
                {
                    OnPropertyChanged(nameof(SelectedRateText));
                    OnPropertyChanged(nameof(CanPurchaseLabel));
                    ((AsyncRelayCommand)PurchaseLabelCommand).NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>The text on the Buy Label button.</summary>
        public string SelectedRateText =>
            SelectedRate != null
                ? $"Buy Label: ${SelectedRate.Cost:F2}"
                : "";

        /// <summary>Can only purchase if we have a rate and an order.</summary>
        public bool CanPurchaseLabel => SelectedRate != null && CurrentOrder != null;

        #endregion

        #region Commands

        public ICommand LoadPendingOrdersCommand { get; }
        public ICommand RefreshRatesCommand { get; }
        public ICommand PurchaseLabelCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Initialization

        public async Task InitializeAsync() => await LoadPendingOrdersAsync();

        private void InitializeDefaults()
        {
            FromName = "My Business";
            FromAddress1 = "123 Main St";
            FromCity = "Grand Rapids";
            FromState = "MI";
            FromZip = "49503";

            UpdateServiceTypes();
            AvailableRates = new ObservableCollection<ShippingRateResult>();
        }

        private void UpdateServiceTypes()
        {
            ServiceTypes.Clear();

            switch (SelectedCarrier)
            {
                case "USPS":
                    ServiceTypes.Add("USPS_GroundAdvantage");
                    ServiceTypes.Add("USPS_Priority");
                    ServiceTypes.Add("USPS_Express");
                    break;

                case "UPS":
                    ServiceTypes.Add("UPS_Ground");
                    ServiceTypes.Add("UPS_2DayAir");
                    ServiceTypes.Add("UPS_NextDayAir");
                    break;

                case "FedEx":
                    ServiceTypes.Add("FedEx_Ground");
                    ServiceTypes.Add("FedEx_TwoDay");
                    ServiceTypes.Add("FedEx_Overnight");
                    break;
            }

            if (ServiceTypes.Any())
                SelectedServiceType = ServiceTypes.First();
        }

        #endregion

        #region eBay API Methods

        private async Task LoadPendingOrdersAsync()
        {
            IsLoading = true;
            Status = "Loading pending eBay orders…";

            try
            {
                _ebayService = _marketplaceFactory
                                 .GetMarketplaceService("eBay")
                             as EbayMarketplaceService;

                if (_ebayService == null || !await _ebayService.IsAuthenticatedAsync())
                {
                    Status = "Not connected to eBay. Please authenticate first.";
                    return;
                }

                var sinceDate = DateTime.UtcNow.AddDays(-7);
                var summaries = await _ebayService.GetRecentSalesAsync(sinceDate);

                PendingOrders.Clear();
                foreach (var s in summaries.Where(x =>
                           x.ShippingStatus.Equals("NotShipped", StringComparison.OrdinalIgnoreCase)
                        || x.ShippingStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                        || string.IsNullOrWhiteSpace(x.ShippingStatus)))
                {
                    PendingOrders.Add(s);
                }

                Status = PendingOrders.Any()
                    ? $"Loaded {PendingOrders.Count} pending orders."
                    : "No pending orders to ship.";
            }
            catch (Exception ex)
            {
                Status = $"Error loading pending orders: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadOrderDetailsAsync(string orderId)
        {
            if (string.IsNullOrWhiteSpace(orderId))
                return;

            IsLoading = true;
            Status = $"Loading details for order {orderId}…";

            try
            {
                var details = await _ebayService.GetOrderDetailsAsync(orderId);
                if (details == null)
                {
                    Status = $"Cannot retrieve details for order {orderId}.";
                    return;
                }

                CurrentOrder = details;
                Status = "Order details loaded. Ready to fetch shipping quotes.";
            }
            catch (Exception ex)
            {
                Status = $"Error loading order details: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshRatesAsync()
        {
            if (CurrentOrder == null) return;

            IsLoading = true;
            Status = "Fetching real eBay shipping quotes…";

            AvailableRates.Clear();
            SelectedRate = null;

            try
            {
                var toZip = ExtractZipFromAddress(CurrentOrder.BuyerAddress);
                Debug.WriteLine($"From ZIP: {FromZip}, To ZIP: {toZip}");
                Debug.WriteLine($"Package: {Length}x{Width}x{Height}, {WeightLbs}lbs {WeightOz}oz");

                var rateRequest = new ShippingRateRequest
                {
                    FromCountry = "US",
                    ToCountry = "US",
                    FromPostalCode = FromZip,
                    ToPostalCode = toZip,
                    Length = double.TryParse(Length, out var l) ? l : 10,
                    Width = double.TryParse(Width, out var w) ? w : 8,
                    Height = double.TryParse(Height, out var h) ? h : 4,
                    WeightLbs = int.TryParse(WeightLbs, out var lbs) ? lbs : 1,
                    WeightOz = int.TryParse(WeightOz, out var oz) ? oz : 0
                };

                if (toZip == "00000" || string.IsNullOrWhiteSpace(toZip))
                {
                    Status = "Error: Could not extract a valid ZIP code from the buyer's address.";
                    return;
                }

                var rates = await _ebayService.GetShippingRatesAsync(rateRequest);
                if (rates == null || !rates.Any())
                {
                    Status = "No shipping quotes returned.";
                    return;
                }

                foreach (var r in rates.OrderBy(r => r.Cost))
                    AvailableRates.Add(r);

                SelectedRate = AvailableRates.First();
                Status = $"Found {AvailableRates.Count} shipping quote(s). Select one to purchase.";
            }
            catch (Exception ex)
            {
                Status = $"Error fetching rates: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task PurchaseLabelAsync()
        {
            if (SelectedRate == null || CurrentOrder == null) return;

            IsLoading = true;
            Status = "Purchasing label from eBay…";

            try
            {
                var labelRequest = new ShippingLabelRequest
                {
                    OrderId = CurrentOrder.OrderId,
                    Carrier = SelectedRate.Carrier,
                    ServiceType = SelectedRate.ServiceName,
                    TrackingNumber = Guid.NewGuid().ToString().Substring(0, 10),
                    WeightLbs = int.TryParse(WeightLbs, out var lbs2) ? lbs2 : 1,
                    WeightOz = int.TryParse(WeightOz, out var oz2) ? oz2 : 0,
                    Length = double.TryParse(Length, out var l2) ? l2 : 10,
                    Width = double.TryParse(Width, out var w2) ? w2 : 8,
                    Height = double.TryParse(Height, out var h2) ? h2 : 4,
                    FromPostalCode = FromZip,
                    ToPostalCode = ExtractZipFromAddress(CurrentOrder.BuyerAddress)
                };

                var result = await _ebayService.CreateShippingLabelAsync(labelRequest);
                Status = result.Success
                    ? $"Label purchased! View/print at: {result.LabelUrl}"
                    : $"Label purchase failed: {result.ErrorMessage}";
            }
            catch (Exception ex)
            {
                Status = $"Error purchasing label: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region Helpers

        private void Cancel()
        {
            SelectedPendingOrder = null;
            CurrentOrder = null;
            AvailableRates.Clear();
            SelectedRate = null;
            Status = "";
        }

        private string ExtractZipFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return "00000";

            var lines = address
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            foreach (var line in lines.Reverse())
            {
                var match = System.Text.RegularExpressions.Regex
                    .Match(line, @"\b(\d{5}(?:-\d{4})?)\b");
                if (match.Success)
                    return match.Groups[1].Value;
            }

            Debug.WriteLine($"Warning: Could not extract ZIP from: {address}");
            return "00000";
        }

        #endregion
    }
}
