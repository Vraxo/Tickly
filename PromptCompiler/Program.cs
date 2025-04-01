using System;
using System.IO;
using System.Linq;
using System.Security; // Required for SecurityElement

namespace CodebaseMerger;

internal static class Program
{
    // --- Configuration ---
    // Define the HARDCODED absolute path to the code's source directory
    private const string HardcodedSourceDirectory = @"D:\Parsa Stuff\Visual Studio\Tickly\Tickly\Source";
    // The name of the output file that will be created in the source directory
    private const string OutputFileName = "MergedCodeForAI.txt";
    // File extensions to include in the merge
    private static readonly string[] IncludeExtensions = [".cs", ".xaml", ".axaml"]; // Add other relevant code file types if needed
    // Directory names (case-insensitive) to exclude completely
    private static readonly string[] ExcludeDirectoryNames = ["bin", "obj", ".vs", "properties", ".git"];
    // Specific file extensions (case-insensitive) to exclude
    private static readonly string[] ExcludeExtensions = [".user", ".csproj", ".sln", ".md", ".txt", ".gitignore", ".gitattributes"];
    // --- End Configuration ---


    public static void Main()
    {
        string sourceDirectoryPath = HardcodedSourceDirectory; // Use the hardcoded path directly
        string outputFilePath = Path.Combine(sourceDirectoryPath, OutputFileName);

        Console.WriteLine($"Source Directory Target: {sourceDirectoryPath}");

        if (!Directory.Exists(sourceDirectoryPath))
        {
            WriteError($"Source directory not found: '{sourceDirectoryPath}'. Please check the 'HardcodedSourceDirectory' constant in the script.");
            return;
        }

        try
        {
            SafelyDeleteFile(outputFilePath);

            Console.WriteLine($"Starting merge process. Output will be saved to: {outputFilePath}");

            using StreamWriter writer = new(outputFilePath);
            int filesProcessed = 0;
            int filesSkipped = 0;

            string[] allFiles = Directory.GetFiles(sourceDirectoryPath, "*.*", SearchOption.AllDirectories);

            foreach (string currentFilePath in allFiles)
            {
                if (ShouldSkipFile(currentFilePath, sourceDirectoryPath, outputFilePath))
                {
                    filesSkipped++;
                    continue;
                }

                try
                {
                    string relativePath = Path.GetRelativePath(sourceDirectoryPath, currentFilePath);
                    // Use simple XML-like tags for structure. Escape path for attribute safety.
                    string escapedRelativePath = SecurityElement.Escape(relativePath) ?? relativePath;
                    string fileContent = File.ReadAllText(currentFilePath);

                    // Write the file block using simple tags
                    writer.WriteLine($"<file path=\"{escapedRelativePath}\">");
                    writer.WriteLine(fileContent.TrimEnd()); // Trim potential trailing whitespace from files
                    writer.WriteLine($"</file>");
                    writer.WriteLine(); // Add a blank line separator for readability

                    filesProcessed++;
                }
                catch (IOException ioEx)
                {
                    WriteWarning($"Could not read file '{currentFilePath}'. Skipping. Error: {ioEx.Message}");
                    filesSkipped++;
                }
                catch (UnauthorizedAccessException authEx)
                {
                    WriteWarning($"Access denied for file '{currentFilePath}'. Skipping. Error: {authEx.Message}");
                    filesSkipped++;
                }
                catch (Exception ex) // Catch unexpected errors during processing of a single file
                {
                    WriteWarning($"Unexpected error processing file '{currentFilePath}'. Skipping. Error: {ex.Message}");
                    filesSkipped++;
                }
            } // End foreach

            Console.WriteLine(); // Blank line before summary
            if (filesProcessed > 0)
            {
                WriteSuccess($"Merging complete. Processed {filesProcessed} files.");
                if (filesSkipped > 0)
                {
                    Console.WriteLine($"Skipped {filesSkipped} files (excluded type, directory, unreadable, or output file).");
                }
            }
            else
            {
                WriteWarning($"No eligible code files were found or processed in '{sourceDirectoryPath}'.");
                if (filesSkipped > 0)
                {
                    Console.WriteLine($"Skipped {filesSkipped} files.");
                }
            }

        }
        catch (Exception ex) // Catch errors during overall process setup (e.g., creating StreamWriter)
        {
            WriteError($"An critical error occurred during the merging process: {ex.Message}");
        }
    }

    private static bool ShouldSkipFile(string filePath, string sourceDirectory, string outputFilePath)
    {
        // Skip the output file itself
        if (string.Equals(filePath, outputFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string fileName = Path.GetFileName(filePath);
        string fileExtension = Path.GetExtension(filePath);
        string? directoryName = Path.GetDirectoryName(filePath);

        // Skip based on directory name
        if (directoryName is not null)
        {
            string[] pathSegments = directoryName.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (pathSegments.Any(segment => ExcludeDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }
        }


        // Skip based on excluded extensions
        if (ExcludeExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Skip if not an included extension (if IncludeExtensions has items)
        if (IncludeExtensions.Length > 0 && !IncludeExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }


        return false; // Do not skip
    }

    private static void SafelyDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Console.WriteLine($"Deleted existing output file: {filePath}");
            }
        }
        catch (IOException ex)
        {
            WriteWarning($"Could not delete existing file '{filePath}'. It might be locked. Error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteWarning($"Could not delete existing file '{filePath}' due to permissions. Error: {ex.Message}");
        }
    }


    // Helper methods for colored console output
    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"ERROR: {message}");
        Console.ResetColor();
    }

    private static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"WARNING: {message}");
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}