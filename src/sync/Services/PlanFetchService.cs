using System.Text.Json;
using sync.Models;

namespace sync.Services;

internal class PlanFetchService
{
    private readonly HttpClient _httpClient;

    public PlanFetchService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<string>> FetchAllPlanIdsAsync(string brand, string baseUrl, int pageSize = 1000, CancellationToken cancellationToken = default)
    {
        var planIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int page = 1;
        int totalPages = 1;

        do
        {
            var endpoint = baseUrl.TrimEnd('/') + $"/cds-au/v1/energy/plans?page-size={pageSize}&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.TryAddWithoutValidation("x-v", "1");
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            PlansListModel? model = null;

            try
            {
                model = await JsonSerializer.DeserializeAsync<PlansListModel>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{brand}] Deserialization error for {endpoint}");
                throw;
            }

            if (model?.Data?.Plans == null)
            {
                Console.WriteLine($"[{brand}] No plans data on page {page}.");
                throw new InvalidDataException($"[{brand}] No plans data found on page {page} for endpoint: {endpoint}");
            }

            foreach (var plan in model.Data.Plans)
            {
                if (!string.IsNullOrWhiteSpace(plan.PlanId)) 
                {
                    planIds.Add(plan.PlanId.Trim());
                }
            }

            if (model.Meta != null)
            {
                totalPages = model.Meta.TotalPages <= 0 ? 1 : model.Meta.TotalPages;
            }

            page++;
        } while (page <= totalPages && !cancellationToken.IsCancellationRequested);

        return planIds.ToList();
    }

    public async Task DownloadPlanAsync(string brand, string baseUrl, string planId, string destinationPath, CancellationToken cancellationToken = default)
    {
        var url = baseUrl.TrimEnd('/') + "/cds-au/v1/energy/plans/" + planId;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-v", "3");
            
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{brand}] ERROR downloading plan from {url}");
            throw;
        }
    }
}
