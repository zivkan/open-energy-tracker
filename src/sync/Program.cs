using sync;
using sync.Services;

try
{
    if (args.Length > 0 && string.Equals(args[0], "update-retailers", StringComparison.Ordinal))
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("Usage: sync update-retailers <output-directory>");
        }

        var outputDirectory = ArgumentValidator.ValidateAndPrepareOutputDirectory(new[] { args[1] });
        var app = new SyncApplication();
        await app.UpdateRetailers(outputDirectory);
        return 0;
    }
    else
    {
        var outputDirectory = ArgumentValidator.ValidateAndPrepareOutputDirectory(args);
        var app = new SyncApplication();
        await app.RunAsync(outputDirectory);
        return 0;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex}");
    return 1;
}
