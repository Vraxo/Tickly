// ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Required for NotifyCollectionChangedEventArgs
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Views;

namespace Tickly.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<TaskItem> _tasks;

        private readonly string _filePath;
        private bool _isSaving = false; // Flag to prevent potential reentry issues
        private object _saveLock = new object(); // For thread safety with the flag

        public MainViewModel()
        {
            _filePath = Path.Combine(FileSystem.AppDataDirectory, "tasks.json");
            _tasks = new ObservableCollection<TaskItem>();
            _tasks.CollectionChanged += Tasks_CollectionChanged;

            // Use await here or ensure LoadTasks handles UI updates correctly if called async void
            // For simplicity in constructor, often better to make LoadTasks synchronous or carefully manage async void
            // Or trigger LoadTasks via an OnAppearing event or similar lifecycle method.
            // Let's assume LoadTasksCommand is okay for now, but be mindful of async void in constructors.
            LoadTasksCommand.Execute(null);

            WeakReferenceMessenger.Default.Register<AddTaskMessage>(this, (r, m) =>
            {
                HandleAddTask(m.Value);
            });
        }

        private async void Tasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Only trigger save on Move action
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                bool shouldSave = false;
                lock (_saveLock)
                {
                    if (!_isSaving)
                    {
                        _isSaving = true;
                        shouldSave = true;
                    }
                }

                if (shouldSave)
                {
                    Debug.WriteLine("Move detected, preparing to save...");
                    // Add a small delay in case multiple move events fire quickly during drag
                    await Task.Delay(250); // Increased delay slightly
                    await SaveTasks();
                    // SaveTasks will reset _isSaving in its finally block
                }
                else
                {
                    Debug.WriteLine("Move detected, but already saving.");
                }
            }
        }

        [RelayCommand]
        private async Task NavigateToAddPage()
        {
            await Shell.Current.GoToAsync(nameof(AddTaskPopupPage), true);
        }

        [RelayCommand]
        private async Task LoadTasks()
        {
            bool lockTaken = false;
            try
            {
                // Prevent loading if a save is somehow in progress
                lock (_saveLock)
                {
                    if (_isSaving)
                    {
                        Debug.WriteLine("LoadTasks skipped, save in progress.");
                        return;
                    }
                    // Optional: Mark as loading if needed
                }


                if (!File.Exists(_filePath))
                {
                    Debug.WriteLine("Task file not found, initializing empty list.");
                    // Ensure Tasks is cleared if file doesn't exist after a previous run
                    if (Tasks.Any())
                    {
                        Tasks.CollectionChanged -= Tasks_CollectionChanged;
                        Tasks.Clear();
                        Tasks.CollectionChanged += Tasks_CollectionChanged;
                    }
                    return;
                }

                Debug.WriteLine("Loading tasks...");
                Tasks.CollectionChanged -= Tasks_CollectionChanged; // Unsubscribe during load

                string json = await File.ReadAllTextAsync(_filePath);
                var loadedTasks = JsonSerializer.Deserialize<List<TaskItem>>(json);

                Tasks.Clear(); // Clear the existing collection

                if (loadedTasks != null)
                {
                    var sortedTasks = loadedTasks.OrderBy(t => t.Order).ToList();
                    foreach (var task in sortedTasks)
                    {
                        Tasks.Add(task);
                    }
                    Debug.WriteLine($"Loaded {Tasks.Count} tasks.");
                }
                else
                {
                    Debug.WriteLine("Task file was empty or invalid JSON.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tasks: {ex.Message}");
                Tasks.Clear(); // Clear on error
            }
            finally
            {
                // Re-subscribe regardless of success or failure
                Tasks.CollectionChanged += Tasks_CollectionChanged;
                // Reset loading flag if used
            }
        }

        // Renamed for clarity, original SaveTasks becomes internal helper
        private async Task SaveTasks()
        {
            Debug.WriteLine("SaveTasks entered...");
            try
            {
                // Update the Order property based on the current list order
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i].Order != i)
                    {
                        Tasks[i].Order = i;
                        Debug.WriteLine($"Updating order for '{Tasks[i].Title}' to {i}");
                    }
                }

                // Create a copy for serialization to avoid collection modified issues if UI updates during save
                var tasksToSave = Tasks.ToList();
                string json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions { WriteIndented = true });

                await File.WriteAllTextAsync(_filePath, json);
                Debug.WriteLine($"Tasks saved successfully to {_filePath}");

#if WINDOWS && DEBUG
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) // Check if directory exists
                {
                    try
                    {
                         Debug.WriteLine($"Opening directory: {directory}");
                         Process.Start(new ProcessStartInfo()
                         {
                             FileName = directory,
                             UseShellExecute = true,
                             Verb = "open"
                         });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to open directory: {ex.Message}");
                    }
                }
                else
                {
                     Debug.WriteLine($"Directory not found or invalid: {directory}");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving tasks: {ex.Message}");
            }
            finally
            {
                // Ensure the flag is reset *after* saving completes or fails
                lock (_saveLock)
                {
                    _isSaving = false;
                }
                Debug.WriteLine("SaveTasks finished, _isSaving reset.");
            }
        }

        private async void HandleAddTask(TaskItem newTask)
        {
            if (newTask != null)
            {
                Debug.WriteLine($"Adding task: {newTask.Title}");
                // No need to unsubscribe/resubscribe, CollectionChanged handles Move only
                newTask.Order = Tasks.Count;
                Tasks.Add(newTask);

                // Trigger save after adding. Use the save flag logic.
                bool shouldSave = false;
                lock (_saveLock)
                {
                    if (!_isSaving)
                    {
                        _isSaving = true;
                        shouldSave = true;
                    }
                }

                if (shouldSave)
                {
                    Debug.WriteLine("Saving after add...");
                    await SaveTasks(); // SaveTasks resets the flag
                }
                else
                {
                    Debug.WriteLine("Skipping save after add, another save is in progress.");
                    // Optionally, queue a save or handle this case if needed
                }
            }
        }
    }
}