using System;
using System.IO;

class Program
{
    static void Main()
    {
        string sourceDirectory = @"D:\Parsa Stuff\Visual Studio\Tickly\Tickly\Source";
        string outputFile = Path.Combine(sourceDirectory, "MergedOutput.txt");

        try
        {
            // Ensure existing output file is deleted before writing
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            using (StreamWriter writer = new StreamWriter(outputFile))
            {
                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    // Skip the output file itself to prevent read/write conflicts
                    if (Path.GetFileName(file).Equals("MergedOutput.txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    writer.WriteLine("// File: " + file.Substring(sourceDirectory.Length + 1));
                    writer.WriteLine(File.ReadAllText(file));
                    writer.WriteLine();
                    writer.WriteLine(new string('-', 80));
                    writer.WriteLine();
                }
            }
            Console.WriteLine("Merging complete! Output file: " + outputFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}