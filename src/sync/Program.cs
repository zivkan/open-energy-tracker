using sync;
using sync.Services;

try
{
    var outputDirectory = ArgumentValidator.ValidateAndPrepareOutputDirectory(args);
    var app = new SyncApplication();
    await app.RunAsync(outputDirectory);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal error: {ex}");
    return 1;
}
