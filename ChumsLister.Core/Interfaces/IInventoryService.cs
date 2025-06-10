// File: ChumsLister.Core/Interfaces/IInventoryService.cs
using ChumsLister.Core.Models;

namespace ChumsLister.Core.Interfaces
{
    public interface IInventoryService
    {
        Task<List<InventoryItem>> GetInventoryItemsAsync();
        Task<InventoryItem> GetInventoryItemAsync(string id);
        Task<InventoryItem> AddInventoryItemAsync(InventoryItem item);
        Task<InventoryItem> UpdateInventoryItemAsync(InventoryItem item);
        Task<bool> DeleteInventoryItemAsync(string id);
        bool DeleteInventoryItem(string id);
        bool ExportToCsv(IEnumerable<InventoryItem> items, string filePath);
        bool ExportToExcel(IEnumerable<InventoryItem> items, string filePath);
        bool ImportFromCsv(string filePath);
        bool ImportFromExcel(string filePath);
    }
}