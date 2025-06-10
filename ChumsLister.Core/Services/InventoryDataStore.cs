using ChumsLister.Core.Models;
using System.IO;
using System.Text.Json;

namespace ChumsLister.Core.Services
{
    public static class InventoryDataStore
    {
        private static readonly string InventoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChumsLister", "inventory.json"
        );

        public static List<InventoryItem> GetAllInventoryItems()
        {
            if (!File.Exists(InventoryPath)) return new List<InventoryItem>();
            var json = File.ReadAllText(InventoryPath);
            return JsonSerializer.Deserialize<List<InventoryItem>>(json) ?? new List<InventoryItem>();
        }

        public static void SaveInventory(List<InventoryItem> items)
        {
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(InventoryPath)!);
            File.WriteAllText(InventoryPath, json);
        }
    }
}
