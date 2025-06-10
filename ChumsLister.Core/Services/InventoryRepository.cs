using ChumsLister.Core.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChumsLister.Core.Services
{
    public static class InventoryRepository
    {
        // Get the database path for the current user
        private static string GetDbPath()
        {
            var userId = UserContext.CurrentUserId ?? "default";
            return DatabaseService.GetDatabasePath(userId);
        }

        public static List<InventoryItem> LoadInventory()
        {
            var items = new List<InventoryItem>();
            var dbPath = GetDbPath();

            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT SKU, TRANS_ID, MODEL_HD_SKU, DESCRIPTION, 
                       QTY, RETAIL_PRICE, COST_ITEM, TOTAL_COST_ITEM, 
                       QTY_SOLD, SOLD_PRICE, STATUS, REPO, LOCATION, DATE_SOLD 
                FROM Inventory";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new InventoryItem
                {
                    SKU = reader.GetString(0),
                    TRANS_ID = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    MODEL_HD_SKU = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    DESCRIPTION = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    QTY = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    RETAIL_PRICE = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5),
                    COST_ITEM = reader.IsDBNull(6) ? 0 : reader.GetDecimal(6),
                    TOTAL_COST_ITEM = reader.IsDBNull(7) ? 0 : reader.GetDecimal(7),
                    QTY_SOLD = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
                    SOLD_PRICE = reader.IsDBNull(9) ? 0 : reader.GetDecimal(9),
                    STATUS = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    REPO = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    LOCATION = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    DATE_SOLD = reader.IsDBNull(13) ? "" : reader.GetString(13),
                });
            }

            Debug.WriteLine($"Loaded {items.Count} inventory items for user {UserContext.CurrentUserId}");
            return items;
        }

        public static void ClearInventory()
        {
            var dbPath = GetDbPath();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Inventory";
            command.ExecuteNonQuery();

            Debug.WriteLine($"Cleared inventory for user {UserContext.CurrentUserId}");
        }

        public static void InsertItem(InventoryItem item)
        {
            var dbPath = GetDbPath();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Inventory (
                    SKU, TRANS_ID, MODEL_HD_SKU, DESCRIPTION, QTY, RETAIL_PRICE, COST_ITEM,
                    TOTAL_COST_ITEM, QTY_SOLD, SOLD_PRICE, STATUS, REPO, LOCATION,
                    DATE_SOLD
                ) VALUES (
                    $sku, $transid, $modelHdSku, $description, $qty, $retailPrice, $costItem,
                    $totalCostItem, $qtySold, $soldPrice, $status, $repo, $location,
                    $dateSold
                )";

            command.Parameters.AddWithValue("$sku", item.SKU ?? "");
            command.Parameters.AddWithValue("$transid", item.TRANS_ID ?? "");
            command.Parameters.AddWithValue("$modelHdSku", item.MODEL_HD_SKU ?? "");
            command.Parameters.AddWithValue("$description", item.DESCRIPTION ?? "");
            command.Parameters.AddWithValue("$qty", item.QTY);
            command.Parameters.AddWithValue("$retailPrice", item.RETAIL_PRICE);
            command.Parameters.AddWithValue("$costItem", item.COST_ITEM);
            command.Parameters.AddWithValue("$totalCostItem", item.TOTAL_COST_ITEM);
            command.Parameters.AddWithValue("$qtySold", item.QTY_SOLD);
            command.Parameters.AddWithValue("$soldPrice", item.SOLD_PRICE);
            command.Parameters.AddWithValue("$status", item.STATUS ?? "");
            command.Parameters.AddWithValue("$repo", item.REPO ?? "");
            command.Parameters.AddWithValue("$location", item.LOCATION ?? "");
            command.Parameters.AddWithValue("$dateSold", item.DATE_SOLD ?? "");

            command.ExecuteNonQuery();
            Debug.WriteLine($"Inserted item {item.SKU} for user {UserContext.CurrentUserId}");
        }

        public static void UpdateItem(InventoryItem item)
        {
            var dbPath = GetDbPath();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Inventory SET 
                    TRANS_ID = $transid,
                    MODEL_HD_SKU = $modelHdSku,
                    DESCRIPTION = $description,
                    QTY = $qty,
                    RETAIL_PRICE = $retailPrice,
                    COST_ITEM = $costItem,
                    TOTAL_COST_ITEM = $totalCostItem,
                    QTY_SOLD = $qtySold,
                    SOLD_PRICE = $soldPrice,
                    STATUS = $status,
                    REPO = $repo,
                    LOCATION = $location,
                    DATE_SOLD = $dateSold
                WHERE SKU = $sku";

            command.Parameters.AddWithValue("$transid", item.TRANS_ID ?? "");
            command.Parameters.AddWithValue("$modelHdSku", item.MODEL_HD_SKU ?? "");
            command.Parameters.AddWithValue("$description", item.DESCRIPTION ?? "");
            command.Parameters.AddWithValue("$qty", item.QTY);
            command.Parameters.AddWithValue("$retailPrice", item.RETAIL_PRICE);
            command.Parameters.AddWithValue("$costItem", item.COST_ITEM);
            command.Parameters.AddWithValue("$totalCostItem", item.TOTAL_COST_ITEM);
            command.Parameters.AddWithValue("$qtySold", item.QTY_SOLD);
            command.Parameters.AddWithValue("$soldPrice", item.SOLD_PRICE);
            command.Parameters.AddWithValue("$status", item.STATUS ?? "");
            command.Parameters.AddWithValue("$repo", item.REPO ?? "");
            command.Parameters.AddWithValue("$location", item.LOCATION ?? "");
            command.Parameters.AddWithValue("$dateSold", item.DATE_SOLD ?? "");
            command.Parameters.AddWithValue("$sku", item.SKU?.Trim() ?? "");

            int rowsAffected = command.ExecuteNonQuery();

            if (rowsAffected == 0)
            {
                Debug.WriteLine($"[UpdateItem] ❌ WARNING: No rows updated for SKU: {item.SKU} (User: {UserContext.CurrentUserId})");
            }
            else
            {
                Debug.WriteLine($"[UpdateItem] ✅ SUCCESS: Updated {rowsAffected} row(s) for SKU: {item.SKU} (User: {UserContext.CurrentUserId})");
            }
        }

        public static void DeleteItem(string sku)
        {
            var dbPath = GetDbPath();
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Inventory WHERE SKU = $sku";
            command.Parameters.AddWithValue("$sku", sku?.Trim() ?? "");

            int rows = command.ExecuteNonQuery();
            Debug.WriteLine($"[DeleteItem] Deleted {rows} row(s) for SKU: {sku} (User: {UserContext.CurrentUserId})");
        }
    }
}