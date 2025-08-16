namespace sync.Services;

/// <summary>
/// Handles command line argument validation and parsing.
/// </summary>
internal static class ArgumentValidator
{
    /// <summary>
    /// Validates and prepares the output directory from command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Full path to the output directory</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid</exception>
    public static string ValidateAndPrepareOutputDirectory(string[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("Usage: sync <output-directory>");
        }

        var outputDirRaw = args[0];
        string outputDirFullPath;
        
        try
        {
            outputDirFullPath = Path.GetFullPath(outputDirRaw);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid output directory path '{outputDirRaw}': {ex.Message}", ex);
        }

        if (File.Exists(outputDirFullPath))
        {
            throw new ArgumentException($"A file already exists at the specified output directory path: '{outputDirFullPath}'");
        }

        if (!Directory.Exists(outputDirFullPath))
        {
            throw new ArgumentException($"Output directory does not exist: '{outputDirFullPath}'");
        }
        else
        {
            Console.WriteLine($"Using existing output directory: {outputDirFullPath}");
        }

        return outputDirFullPath;
    }
}