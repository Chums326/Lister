using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows;
using System.Linq;

namespace ChumsLister.WPF.Services
{
    public class BackgroundTaskService
    {
        private CancellationTokenSource _cts;
        private readonly ObservableCollection<BackgroundTask> _tasks;

        public BackgroundTaskService()
        {
            _tasks = new ObservableCollection<BackgroundTask>();
        }

        public ObservableCollection<BackgroundTask> Tasks => _tasks;

        public BackgroundTask StartBulkImportTask(
            string csvFilePath,
            List<string> targetPlatforms,
            Core.Services.BulkListingService bulkListingService)
        {
            var task = new BackgroundTask
            {
                Name = "Bulk Import",
                Description = $"Importing products from {System.IO.Path.GetFileName(csvFilePath)}",
                StartTime = DateTime.Now,
                Status = BackgroundTaskStatus.Running,
                Progress = 0
            };

            _tasks.Add(task);

            // Start background task
            _cts = new CancellationTokenSource();
            var cancellationToken = _cts.Token;

            // Create progress reporter
            var progress = new Progress<int>(percent =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Progress = percent;
                });
            });

            Task.Run(async () =>
            {
                try
                {
                    // Process file
                    var results = await bulkListingService.ImportFromCsvAsync(
                        csvFilePath, targetPlatforms, progress);

                    // Calculate successful/failed counts
                    int successCount = results.Count(r => r.Success);
                    int failedCount = results.Count - successCount;

                    // Update task when complete
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        task.Status = BackgroundTaskStatus.Completed;
                        task.Progress = 100;
                        task.EndTime = DateTime.Now;
                        task.Description = $"Imported {successCount} products successfully ({failedCount} failed)";
                        task.Results = results;
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        task.Status = BackgroundTaskStatus.Failed;
                        task.EndTime = DateTime.Now;
                        task.Description = $"Import failed: {ex.Message}";
                    });
                }
            }, cancellationToken);

            return task;
        }

        public BackgroundTask StartInventorySyncTask(Core.Services.InventorySyncService inventorySyncService)
        {
            var task = new BackgroundTask
            {
                Name = "Inventory Sync",
                Description = "Synchronizing inventory across all platforms",
                StartTime = DateTime.Now,
                Status = BackgroundTaskStatus.Running,
                Progress = 0
            };

            _tasks.Add(task);

            // Start background task
            _cts = new CancellationTokenSource();
            var cancellationToken = _cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    // Update progress periodically
                    var progressTimer = new System.Threading.Timer(state =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (task.Progress < 90)
                                task.Progress += 10;
                        });
                    }, null, 1000, 2000);

                    // Perform sync
                    var results = await inventorySyncService.SyncInventoryAcrossPlatformsAsync();

                    // Clean up timer
                    progressTimer.Dispose();

                    // Calculate stats
                    int successCount = results.Count(r => r.Success);
                    int platformCount = results.Select(r => r.PlatformName).Distinct().Count();

                    // Update task when complete
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        task.Status = BackgroundTaskStatus.Completed;
                        task.Progress = 100;
                        task.EndTime = DateTime.Now;
                        task.Description = $"Synced inventory across {platformCount} platforms ({successCount}/{results.Count} operations successful)";
                        task.Results = results;
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        task.Status = BackgroundTaskStatus.Failed;
                        task.EndTime = DateTime.Now;
                        task.Description = $"Sync failed: {ex.Message}";
                    });
                }
            }, cancellationToken);

            return task;
        }

        public void CancelTask(BackgroundTask task)
        {
            if (task.Status == BackgroundTaskStatus.Running && _cts != null)
            {
                _cts.Cancel();

                task.Status = BackgroundTaskStatus.Canceled;
                task.EndTime = DateTime.Now;
                task.Description += " (Cancelled by user)";
            }
        }

        public void ClearCompletedTasks()
        {
            for (int i = _tasks.Count - 1; i >= 0; i--)
            {
                if (_tasks[i].Status != BackgroundTaskStatus.Running)
                {
                    _tasks.RemoveAt(i);
                }
            }
        }
    }

    public class BackgroundTask
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public BackgroundTaskStatus Status { get; set; }
        public int Progress { get; set; }
        public object Results { get; set; }
    }

    // Custom enum to avoid confusion with System.Threading.Tasks.TaskStatus
    public enum BackgroundTaskStatus
    {
        Created,
        Running,
        Completed,
        Failed,
        Canceled
    }
}