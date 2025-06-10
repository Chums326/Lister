using ChumsLister.Core.Models;
using ExcelDataReader;
using System.Data;
using System.IO;

namespace ChumsLister.Core.Services
{
    public static class ExcelImporter
    {
        public static List<InventoryItem> ImportInventoryFromExcel(string filePath)
        {
            var items = new List<InventoryItem>();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            var table = result.Tables[0];

            foreach (DataRow row in table.Rows)
            {
                try
                {
                    var item = new InventoryItem
                    {
                        SKU = row["SKU"]?.ToString()?.Trim() ?? string.Empty,
                        TRANS_ID = row.Table.Columns.Contains("TRANS_ID") ? row["TRANS_ID"]?.ToString() : string.Empty,
                        MODEL_HD_SKU = row.Table.Columns.Contains("MODEL_HD_SKU") ? row["MODEL_HD_SKU"]?.ToString() : string.Empty,
                        DESCRIPTION = row.Table.Columns.Contains("DESCRIPTION") ? row["DESCRIPTION"]?.ToString() : string.Empty,
                        QTY = TryParseInt(row, "QTY"),
                        RETAIL_PRICE = TryParseDecimal(row, "RETAIL_PRICE"),
                        COST_ITEM = TryParseDecimal(row, "COST_ITEM"),
                        TOTAL_COST_ITEM = TryParseDecimal(row, "TOTAL_COST_ITEM"),
                        QTY_SOLD = TryParseInt(row, "QTY_SOLD"),
                        SOLD_PRICE = TryParseDecimal(row, "SOLD_PRICE"),
                        STATUS = row.Table.Columns.Contains("STATUS") ? row["STATUS"]?.ToString() : string.Empty,
                        REPO = row.Table.Columns.Contains("REPO") ? row["REPO"]?.ToString() : string.Empty,
                        LOCATION = row.Table.Columns.Contains("LOCATION") ? row["LOCATION"]?.ToString() : string.Empty,
                        DATE_SOLD = row.Table.Columns.Contains("DATE_SOLD") ? row["DATE_SOLD"]?.ToString() : string.Empty,
                    };

                    if (!string.IsNullOrWhiteSpace(item.SKU))
                        items.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to parse row: " + ex.Message);
                }
            }

            return items;
        }

        private static int TryParseInt(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName) && int.TryParse(row[columnName]?.ToString(), out int value))
                return value;
            return 0;
        }

        private static decimal TryParseDecimal(DataRow row, string columnName)
        {
            if (row.Table.Columns.Contains(columnName))
            {
                var raw = row[columnName]?.ToString()?.Replace("$", "").Replace(",", "").Trim();
                if (decimal.TryParse(raw, out decimal value))
                    return value;
            }
            return 0m;
        }
    }
}
