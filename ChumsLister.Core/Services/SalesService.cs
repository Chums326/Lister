using ChumsLister.Core.Models;
using System.IO;
using System.Text.Json;

namespace ChumsLister.Core.Services
{
    public static class SalesService
    {
        private static readonly string SalesLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChumsLister", "sales.json"
        );

        public static void RecordSale(SaleTransaction transaction)
        {
            var sales = LoadAllSales();
            sales.Add(transaction);
            var json = JsonSerializer.Serialize(sales, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(SalesLogPath)!);
            File.WriteAllText(SalesLogPath, json);
        }

        public static List<SaleTransaction> LoadAllSales()
        {
            if (!File.Exists(SalesLogPath))
                return new List<SaleTransaction>();

            var json = File.ReadAllText(SalesLogPath);
            return JsonSerializer.Deserialize<List<SaleTransaction>>(json) ?? new List<SaleTransaction>();
        }
    }
}
