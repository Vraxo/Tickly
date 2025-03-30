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
                foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(file).Equals("MergedOutput.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    writer.WriteLine("// * File: " + file[(sourceDirectory.Length + 1)..] + "---");
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