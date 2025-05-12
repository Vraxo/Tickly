# Tickly Tickly 📝

Tickly is a straightforward task management application built with .NET MAUI. It's designed to help you keep track of your to-dos with support for due dates, recurring tasks, and basic progress visualization.

## Features 🚀

*   **Task Management ✅:**
    *   Create, edit, delete, and reorder tasks.
    *   Support for due dates, timeless tasks, and recurring schedules (daily, alternate day, weekly).
    *   Visual indicators for task priority/order using a color gradient 🌈.
*   **Progress & Stats 📊:**
    *   Track daily task completion with a dynamic progress bar.
    *   View historical completion percentages on a bar chart, with selectable time ranges (last 7 days, 30 days, etc.).
*   **Customization & Localization 🎨🌍:**
    *   Choose from a variety of themes, including:
        *   Dark 🌃: PitchBlack, Dark Gray, Nord, Catppuccin Mocha, Solarized Dark, Gruvbox Dark, Monokai
        *   Light ☀️: Default Light, Solarized Light, Sepia
        *   Accessibility ♿: High Contrast Dark, High Contrast Light
    *   Select your preferred calendar system for date display (Gregorian or Persian/Shamsi).
*   **Data Management 💾:**
    *   Full app data backup (tasks, settings, progress) to a single JSON file.
    *   Import data from a backup file, replacing current data after confirmation.
    *   Export daily progress history to a plain text file.
    *   Option to reset all stored progress data 🗑️.
*   **Windows Specific 🪟:**
    *   The application launches with a fixed, centered window for a consistent desktop experience.

## Tech Stack 🛠️

*   .NET MAUI
*   CommunityToolkit.Mvvm

This project focuses on core task management functionalities with an emphasis on user-selectable theming and basic data portability.