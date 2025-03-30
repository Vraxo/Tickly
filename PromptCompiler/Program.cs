class Program
{
    public static void Main()
    {
        string sourceDirectory = @"D:\Parsa Stuff\Visual Studio\Tickly\Tickly\Source";
        string outputFile = Path.Combine(sourceDirectory, "MergedOutput.txt");

        try
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            using (StreamWriter writer = new(outputFile))
            {
                writer.WriteLine("Here's the entire code base of Tickly.");
                writer.WriteLine("On top of each file, its name is written.");
                writer.WriteLine("You might see the name written twice. That's a mistake.");
                writer.WriteLine("It's a leftover of when file names were written in the file itself.");
                writer.WriteLine("Exclude the file names when rewriting files. Those are for reference only and should not be part of the actual file content!");

                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).Equals("MergedOutput.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    writer.WriteLine("// File: " + file[(sourceDirectory.Length + 1)..]);
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