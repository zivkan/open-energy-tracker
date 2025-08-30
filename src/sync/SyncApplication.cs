using System.Net.Http.Headers;
using sync.Models;
using sync.Services;
using System.Text.Json;
using System.Linq;

namespace sync;

internal class SyncApplication
{
    private readonly PlanFetchService _planFetchService;
    private readonly Retailers _retailers;

    public SyncApplication()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("open-energy-tracker (https://github.com/zivkan/open-energy-tracker)");
        
        _planFetchService = new PlanFetchService(httpClient);
        _retailers = new Retailers(httpClient);
    }

    public async Task RunAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        // Load retailers from retailers.json in the output directory
        var retailersFile = Path.Combine(outputDirectory, "retailers.json");
        if (!File.Exists(retailersFile))
        {
            throw new ArgumentException($"Missing retailers.json in '{outputDirectory}'. Run 'sync update-retailers <output-directory>' first.");
        }

        List<(string brand, string baseUrl)> retailers;
        await using (var fs = File.OpenRead(retailersFile))
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var entries = await JsonSerializer.DeserializeAsync<List<RetailerEntry>>(fs, options, cancellationToken) 
                          ?? new List<RetailerEntry>();
            retailers = entries.Select(e => (e.Brand, e.BaseUrl)).ToList();
        }

        Console.WriteLine($"Retailer count: {retailers.Count}");
        Console.WriteLine("");

        // Collect all plan IDs per retailer
        Console.WriteLine("::group::Get plan list for all retailers");
        List<RetailerPlan> retailerPlans;
        try
        {
            retailerPlans = await CollectRetailerPlansAsync(retailers, cancellationToken);
        }
        finally
        {
            Console.WriteLine("::endgroup::");
        }

        Console.WriteLine();

        // Download missing plan files
        Console.WriteLine("::group::Download missing plan files");
        try
        {
            await DownloadMissingPlansAsync(retailerPlans, outputDirectory, cancellationToken);
        }
        finally
        {
            Console.WriteLine("::endgroup::");
        }
    }

    public async Task UpdateRetailers(string outputDirectory, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("::group::Update retailers");
        try
        {
            var retailers = await _retailers.GetRetailersAsync();
            Console.WriteLine($"Retailer count: {retailers.Count}");

            var output = retailers
                .Select(r => new { Brand = r.brand, BaseUrl = r.baseUrl })
                .OrderBy(r => r.Brand)
                .ToList();

            var filePath = Path.Combine(outputDirectory, "retailers.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            await using var fs = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fs, output, options, cancellationToken);
            Console.WriteLine($"Wrote retailers to {filePath}");
        }
        finally
        {
            Console.WriteLine("::endgroup::");
        }
    }

    private async Task<List<RetailerPlan>> CollectRetailerPlansAsync(List<(string brand, string baseUrl)> retailers, CancellationToken cancellationToken)
    {
        var retailerPlans = new List<RetailerPlan>();

        foreach (var (brand, baseUrl) in retailers)
        {
            var planIds = await _planFetchService.FetchAllPlanIdsAsync(brand, baseUrl, cancellationToken: cancellationToken);
            retailerPlans.Add(new RetailerPlan(brand, baseUrl, planIds));
            Console.WriteLine($"[{brand}] Total distinct plans: {planIds.Count}");
        }

        return retailerPlans;
    }

    private async Task DownloadMissingPlansAsync(List<RetailerPlan> retailerPlans, string outputDirectory, CancellationToken cancellationToken)
    {
        var totalPlans = retailerPlans.Sum(r => r.PlanIds.Count);
        int downloadedCount = 0;
        int existingCount = 0;
        int checkedCount = 0;
        TimeSpan reportInterval = TimeSpan.FromMinutes(1);
        DateTime nextReport = DateTime.UtcNow.Add(reportInterval);

        for (int i = 0; i < retailerPlans.Count; i++)
        {
            var retailer = retailerPlans[i];

            var brandOutput = Path.Combine(outputDirectory, retailer.Brand);
            if (!Directory.Exists(brandOutput)) 
            {
                Directory.CreateDirectory(brandOutput);
            }

            foreach (var planId in retailer.PlanIds)
            {
                var filePath = Path.Combine(brandOutput, $"{planId}.json");
                
                if (File.Exists(filePath))
                {
                    existingCount++;
                }
                else
                {
                    await _planFetchService.DownloadPlanAsync(retailer.Brand, retailer.BaseUrl, planId, filePath, cancellationToken);
                    if (File.Exists(filePath))
                    {
                        downloadedCount++;
                    }
                }

                checkedCount++;
                if (DateTime.UtcNow >= nextReport)
                {
                    int percentageComplete = checkedCount * 100 / totalPlans;
                    Console.WriteLine($"Progress: {checkedCount}/{totalPlans} ({percentageComplete}%) plans checked, {downloadedCount} downloaded, {existingCount} existing.");
                    nextReport = DateTime.UtcNow.Add(reportInterval);
                }
            }

            Console.WriteLine($"{i+1}/{retailerPlans.Count} retailers complete.");
        }
    }

    private sealed record RetailerEntry(string Brand, string BaseUrl);
}
