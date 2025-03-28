// ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Storage;
using System.ComponentModel;
using System.Diagnostics;
using Tickly.Messages;
using Tickly.Models;
using Tickly.Services;
using System.Collections.Generic; // For EqualityComparer

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
            LoadSettings(); // Now just syncs ViewModel bools with AppSettings
            this.PropertyChanged += SettingsViewModel_PropertyChanged;
        }

        // --- MODIFIED LoadSettings ---
        private void LoadSettings()
        {
            // Read the *already initialized* value from the static AppSettings
            var currentSystem = AppSettings.SelectedCalendarSystem;
            Debug.WriteLine($"SettingsViewModel: Loading initial state from AppSettings. CurrentSystem='{currentSystem}'");

            // Update the boolean properties for RadioButton binding based on the static value
            // Use UpdateProperty helper to set initial values without triggering save/notify logic unnecessarily
            UpdateProperty(ref _isGregorianSelected, currentSystem == CalendarSystemType.Gregorian, nameof(IsGregorianSelected));
            UpdateProperty(ref _isPersianSelected, currentSystem == CalendarSystemType.Persian, nameof(IsPersianSelected));
        }
        // --- End MODIFIED LoadSettings ---

        // PropertyChanged handler remains the same (updates AppSettings, saves Prefs, sends message)
        private void SettingsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            CalendarSystemType newSystem;

            if (e.PropertyName == nameof(IsGregorianSelected) && IsGregorianSelected)
            {
                newSystem = CalendarSystemType.Gregorian;
                if (IsPersianSelected) UpdateProperty(ref _isPersianSelected, false, nameof(IsPersianSelected));
            }
            else if (e.PropertyName == nameof(IsPersianSelected) && IsPersianSelected)
            {
                newSystem = CalendarSystemType.Persian;
                if (IsGregorianSelected) UpdateProperty(ref _isGregorianSelected, false, nameof(IsGregorianSelected));
            }
            else { return; }

            if (AppSettings.SelectedCalendarSystem != newSystem)
            {
                Debug.WriteLine($"SettingsViewModel: PropertyChanged detected user change to {newSystem}. Saving and notifying.");
                // Update static setting FIRST
                AppSettings.SelectedCalendarSystem = newSystem;
                // Save to Preferences
                Preferences.Set(AppSettings.CalendarSystemKey, (int)newSystem);
                // Send notification message
                WeakReferenceMessenger.Default.Send(new CalendarSettingChangedMessage());
                Debug.WriteLine("SettingsViewModel: Sent CalendarSettingChangedMessage.");
            }
        }

        // Helper to update backing field and raise PropertyChanged if value changed
        protected bool UpdateProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}