using System.Collections.ObjectModel;
using System.Linq;
using ChumsLister.Core.Models;
using ChumsLister.Core.Services;

namespace ChumsLister.WPF.Services
{
    /// <summary>
    /// InventoryService manages the in-memory collection and persists via InventoryRepository.
    /// Implements a singleton Instance for legacy code that references InventoryService.Instance.
    /// </summary>
    public class InventoryService
    {
        /// <summary>
        /// Legacy GetInstance for pages still calling GetInstance
        /// </summary>
        public static InventoryService GetInstance(string userId) => Instance;

        /// <summary>
        /// Singleton instance (set when DI container constructs this service).
        /// </summary>
        public static InventoryService Instance { get; private set; }

        /// <summary>
        /// Public parameterless constructor for DI container.
        /// Sets the static Instance and subscribes to user changes.
        /// </summary>
        public InventoryService()
        {
            Instance = this;

            // Subscribe to user changes to clear inventory when user switches
            UserContext.OnUserChanged += OnUserChanged;
        }

        /// <summary>
        /// Backing collection for UI binding.
        /// </summary>
        public ObservableCollection<InventoryItem> InventoryItems { get; private set; } = new ObservableCollection<InventoryItem>();

        /// <summary>
        /// Called when user changes - clears the in-memory inventory
        /// </summary>
        private void OnUserChanged(string newUserId)
        {
            // Clear the in-memory collection when user changes
            InventoryItems.Clear();

            // Also clear the static state in InventoryPage if it exists
            ChumsLister.WPF.Views.InventoryPage.ClearStaticInventory();
        }

        /// <summary>
        /// Loads all items from the repository into the ObservableCollection.
        /// </summary>
        public void LoadInventory()
        {
            var items = InventoryRepository.LoadInventory();
            InventoryItems = new ObservableCollection<InventoryItem>(items);
        }

        /// <summary>
        /// Adds an item to both the repository and the in-memory collection.
        /// </summary>
        public void AddItem(InventoryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SKU)) return;
            InventoryRepository.InsertItem(item);
            InventoryItems.Add(item);
        }

        /// <summary>
        /// Removes an item by SKU from both the repository and in-memory collection.
        /// </summary>
        public void RemoveItem(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return;
            InventoryRepository.DeleteItem(sku);
            var toRemove = InventoryItems.FirstOrDefault(x => x.SKU == sku);
            if (toRemove != null)
                InventoryItems.Remove(toRemove);
        }

        /// <summary>
        /// Updates an existing item in repository and in-memory.
        /// </summary>
        public void UpdateItem(InventoryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SKU)) return;
            InventoryRepository.UpdateItem(item);
            var existing = InventoryItems.FirstOrDefault(x => x.SKU == item.SKU);
            if (existing != null)
            {
                var idx = InventoryItems.IndexOf(existing);
                InventoryItems[idx] = item;
            }
        }

        /// <summary>
        /// Cleanup method to unsubscribe from events
        /// </summary>
        public void Dispose()
        {
            UserContext.OnUserChanged -= OnUserChanged;
        }
    }
}