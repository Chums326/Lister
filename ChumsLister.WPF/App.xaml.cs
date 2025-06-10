// App.xaml.cs
using ChumsLister.Core.Interfaces;
using ChumsLister.Core.Services;
using ChumsLister.Core.Services.Marketplaces;
using ChumsLister.WPF.Services;
using ChumsLister.WPF.ViewModels;
using ChumsLister.WPF.Views;
using ChumsLister.WPF.Utilities;          
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Windows;                     

namespace ChumsLister.WPF
{
    public partial class App : System.Windows.Application
    {
        private IServiceProvider _serviceProvider;
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1) Build IConfiguration (reads appsettings.json)
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // 2) ConfigureServices → IServiceCollection → BuildServiceProvider
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);

            // 3) Core ISettingsService (singleton that loads/saves UserSettings to disk)
            services.AddSingleton<ISettingsService>(sp =>
                ChumsLister.Core.Services.SettingsManager.Instance);

            // 4) OAuth helper & token store for eBay
            services.AddSingleton<EbayOAuthHelper>();
            services.AddSingleton<IEbayTokenStore, EbayTokenStore>();

            // 5) eBay marketplace service (typed HttpClient)
            services.AddHttpClient<EbayMarketplaceService>()
                    .SetHandlerLifetime(TimeSpan.FromMinutes(5));
            services.AddSingleton<IMarketplaceService, EbayMarketplaceService>();
            services.AddSingleton<EbayMarketplaceService>();
            services.AddSingleton<IMarketplaceService>(sp => sp.GetRequiredService<EbayMarketplaceService>());
            services.AddSingleton<IEbayService>(sp => sp.GetRequiredService<EbayMarketplaceService>());

            // 6) Marketplace factory (so you can get "eBay" by name)
            services.AddSingleton<IMarketplaceServiceFactory, MarketplaceServiceFactory>();

            // 7) Other core services
            services.AddSingleton<IProductScraper, ChumsLister.Core.Services.ProductScraperService>();
            services.AddSingleton<IAIService, ChumsLister.Core.Services.AnthropicAIService>();
            services.AddSingleton<ChumsLister.Core.Services.AIDescriptionGeneratorService>();
            services.AddSingleton<ChumsLister.Core.Services.MultiPlatformPublishingService>();
            services.AddSingleton<ChumsLister.Core.Services.BulkListingService>();
            services.AddSingleton<ChumsLister.Core.Services.PriceOptimizationService>();
            services.AddSingleton<ChumsLister.Core.Services.InventorySyncService>();

            // 8) Register InventoryService so DI can supply it
            services.AddSingleton<InventoryService>();

            // 9) WPF‐specific services
            services.AddSingleton<BackgroundTaskService>();
            services.AddSingleton<ChumsLister.Core.Services.OrderTrackingService>();

            // 10) WPF Views & ViewModels
            
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<ShippingViewModel>();
            services.AddTransient<ShippingPage>(sp =>
            {
                var vm = sp.GetRequiredService<ShippingViewModel>();
                return new ShippingPage(vm);
            });
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardPage>(sp =>
            {
                var vm = sp.GetRequiredService<DashboardViewModel>();
                return new DashboardPage(vm);
            });            
            services.AddTransient<POSPage>();            
            services.AddTransient<InventoryPage>(sp =>
            {
                var invSvc = sp.GetRequiredService<InventoryService>();
                return new InventoryPage(invSvc);
            });

            services.AddTransient<SettingsPage>(sp =>
            {
                var settingsSvc = sp.GetRequiredService<ISettingsService>();
                var marketplaceFactory = sp.GetService<IMarketplaceServiceFactory>();
                var config = sp.GetRequiredService<IConfiguration>();
                var ebayTokenStore = sp.GetRequiredService<IEbayTokenStore>();
                return new SettingsPage(settingsSvc, marketplaceFactory, config, ebayTokenStore);
            });

            services.AddTransient<OrdersPage>(sp =>
            {
                var orderSvc = sp.GetRequiredService<ChumsLister.Core.Services.OrderTrackingService>();
                return new OrdersPage(orderSvc);
            });

            services.AddTransient<ListingsPage>(sp =>
            {
                var scraper = sp.GetRequiredService<IProductScraper>();
                var factory = sp.GetRequiredService<IMarketplaceServiceFactory>();
                var aiSvc = sp.GetRequiredService<IAIService>();
                return new ListingsPage(scraper, factory, aiSvc, sp);
            });

            services.AddTransient<Views.Wizards.WizardWindow>(sp =>
            {
                var scraper = sp.GetRequiredService<IProductScraper>();
                var ebayService = sp.GetRequiredService<IEbayService>();
                var aiSvc = sp.GetRequiredService<IAIService>();
                return new Views.Wizards.WizardWindow(scraper, ebayService, aiSvc);
            });

            // 11) Configure shutdown mode
            Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

            // 12) Build the IServiceProvider
            _serviceProvider = services.BuildServiceProvider();
            ServiceProvider = _serviceProvider;

            // 13) Initialize or migrate your database if needed
            ChumsLister.Core.Services.DatabaseService.InitializeDatabase();

            // 14) Attempt auto‐login; if it succeeds, it should have already set MainWindow and shown it.
            var loginVM = ServiceProvider.GetRequiredService<LoginViewModel>();
            if (loginVM.TryAutoLogin())
            {
                return; // Auto‐login succeeded; do not show a second window.
            }

            // 15) Otherwise, show the login page exactly once
            var loginWindow = new LoginPage { DataContext = loginVM };
            MainWindow = loginWindow;
            loginWindow.Show();
        }

        /// <summary>
        /// Call this after a successful login:
        ///  1) Set UserContext.CurrentUserId (for Database/Inventory).
        ///  2) Set ISettingsService.CurrentUserId (so Load/Save use that user's file).
        ///  3) Call IEbayTokenStore.SetCurrentUser (so any eBay calls read/write the correct tokens).
        ///  4) Apply the user's theme.
        /// </summary>
        public void SetCurrentUser(string userId)
        {
            Debug.WriteLine($"[App] SetCurrentUser: {userId}");

            // 1) Set user context for database/inventory code:
            ChumsLister.Core.Services.UserContext.CurrentUserId = userId;
            ChumsLister.Core.Services.DatabaseService.SetCurrentUser(userId);
            ChumsLister.Core.Services.DatabaseService.InitializeDatabase(userId);

            // 2) Set user ID in settings service
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            settingsService.CurrentUserId = userId;

            // 3) Set user ID in the EbayTokenStore
            var tokenStore = _serviceProvider.GetRequiredService<IEbayTokenStore>();
            tokenStore.SetCurrentUser(userId);

            // 4) Apply the theme for this user
            InitializeTheme();
        }

        private void InitializeTheme()
        {
            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                if (!string.IsNullOrEmpty(settingsService.CurrentUserId))
                {
                    var userSettings = settingsService.GetSettings();
                    bool darkMode = userSettings.UseDarkMode;

                    // ThemeManager lives in ChumsLister.WPF.Utilities
                    ThemeManager.ApplyTheme(darkMode);

                    Helpers.AppSettings.SyncWithCoreSettings(userSettings);
                    Debug.WriteLine($"[App] Theme initialized for user {settingsService.CurrentUserId}: DarkMode={darkMode}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error initializing theme: {ex}");
            }

            // Fallback if no user or on error:
            bool fallbackDark = Helpers.AppSettings.DarkMode;
            ThemeManager.ApplyTheme(fallbackDark);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                Debug.WriteLine("[App] Application shutting down...");
                ChumsLister.Core.Services.UserContext.Clear();
                Debug.WriteLine("[App] Application shutdown complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Error during shutdown: {ex}");
            }

            base.OnExit(e);
        }
    }
}
