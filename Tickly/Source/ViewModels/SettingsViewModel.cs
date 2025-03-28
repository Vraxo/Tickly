// ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging; // <-- Add this
using Microsoft.Maui.Storage;
using System.ComponentModel;
using System.Diagnostics; // <-- Add this if not present
using Tickly.Messages; // <-- Add this
using Tickly.Models;
using Tickly.Services;

namespace Tickly.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isGregorianSelected;

        [ObservableProperty]
        private bool _isPersianSelected;

        public SettingsViewModel()
        {
            LoadSettings();
            this.PropertyChanged += SettingsViewModel_PropertyChanged;
        }

        private void LoadSettings()
        {
            int storedValue = Preferences.Get(AppSettings.CalendarSystemKey, (int)CalendarSystemType.Gregorian);
            var loadedSystem = (CalendarSystemType)storedValue;
            Debug.WriteLine($"SettingsViewModel: Loading setting - StoredValue={storedValue}, LoadedSystem={loadedSystem}");

            AppSettings.SelectedCalendarSystem = loadedSystem;

            // Use OnPropertyChanged directly to avoid triggering save during load
            OnPropertyChanged(nameof(IsGregorianSelected));
            OnPropertyChanged(nameof(IsPersianSelected));
            _isGregorianSelected = loadedSystem == CalendarSystemType.Gregorian;
            _isPersianSelected = loadedSystem == CalendarSystemType.Persian;
            OnPropertyChanged(nameof(IsGregorianSelected));
            OnPropertyChanged(nameof(IsPersianSelected));
        }

        private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            CalendarSystemType newSystem;

            // Determine the new system based on which boolean property changed *to true*
            if (e.PropertyName == nameof(IsGregorianSelected) && IsGregorianSelected)
            {
                newSystem = CalendarSystemType.Gregorian;
                if (IsPersianSelected) UpdateProperty(ref _isPersianSelected, false, nameof(IsPersianSelected)); // Use helper to avoid loop
            }
            else if (e.PropertyName == nameof(IsPersianSelected) && IsPersianSelected)
            {
                newSystem = CalendarSystemType.Persian;
                if (IsGregorianSelected) UpdateProperty(ref _isGregorianSelected, false, nameof(IsGregorianSelected)); // Use helper to avoid loop
            }
            else
            {
                return; // Only act when a selection becomes true
            }

            // Check if the system actually changed from the currently loaded setting
            if (AppSettings.SelectedCalendarSystem != newSystem)
            {
                Debug.WriteLine($"SettingsViewModel: PropertyChanged detected change to {newSystem}. Saving and notifying.");

                // Save to Preferences
                Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);

                // Update the static AppSettings immediately
                AppSettings.SelectedCalendarSystem = newSystem;

                // *** SEND NOTIFICATION MESSAGE ***
                WeakReferenceMessenger.Default.Send(new CalendarSettingChangedMessage());
                Debug.WriteLine("SettingsViewModel: Sent CalendarSettingChangedMessage.");
            }
        }

        // Helper to update backing field and raise PropertyChanged, avoiding infinite loops
        // (Can be added to ObservableObject base class or kept here)
        protected bool UpdateProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}