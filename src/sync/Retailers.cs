using HtmlAgilityPack;
using UglyToad.PdfPig;
using Tabula;
using Tabula.Extractors;

namespace sync
{
    internal class Retailers
    {
        private readonly HttpClient _httpClient;

        public Retailers(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<(string brand, string baseUrl)>> GetRetailersAsync()
        {
            // Get the PDF link
            var pdfUrl = await GetPdfLinkAsync();
            if (pdfUrl == null)
                throw new InvalidOperationException("Could not find PDF link on the AER page.");

            // Download the PDF
            var pdfBytes = await DownloadPdfAsync(pdfUrl);
            if (pdfBytes == null || pdfBytes.Length == 0)
                throw new InvalidOperationException("Failed to download PDF or PDF is empty.");

            // Find the CHANGE LOG page
            var changeLogPage = FindChangeLogPage(pdfBytes);
            if (changeLogPage == null)
                throw new InvalidOperationException("Could not find 'CHANGE LOG' page in the PDF.");

            // Extract retailers using Tabula (table extraction)
            var retailers = GetRetailersFromPdfUsingTabula(pdfBytes, changeLogPage.Value);
            if (retailers.Count == 0)
                throw new InvalidOperationException("No retailers found via Tabula before the 'CHANGE LOG' page.");

            return retailers;
        }

        private async Task<string?> GetPdfLinkAsync()
        {
            var url = "https://www.aer.gov.au/documents/consumer-data-right-energy-retailer-base-uris-and-cdr-brands";
            var html = await _httpClient.GetStringAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find the first PDF link in the HTML
            var pdfNode = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.pdf')]");
            string? href = pdfNode?.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrEmpty(href)) return null;

            // Calculate absolute URL
            var pdfUri = new Uri(href, UriKind.RelativeOrAbsolute);
            if (!pdfUri.IsAbsoluteUri)
            {
                var baseUri = new Uri(url);
                pdfUri = new Uri(baseUri, pdfUri);
            }
            return pdfUri.ToString();
        }

        private async Task<byte[]> DownloadPdfAsync(string pdfUrl)
        {
            return await _httpClient.GetByteArrayAsync(pdfUrl);
        }

        private static int? FindChangeLogPage(byte[] pdfBytes)
        {
            using var pdfStream = new MemoryStream(pdfBytes);
            using var pdf = PdfDocument.Open(pdfStream);
            int pageNumber = 1;
            foreach (var page in pdf.GetPages())
            {
                var text = page.Text;
                if (text.Contains("CHANGE LOG", StringComparison.OrdinalIgnoreCase))
                {
                    return pageNumber;
                }
                pageNumber++;
            }
            return null;
        }

        private static List<(string brand, string baseUrl)> GetRetailersFromPdfUsingTabula(byte[] pdfBytes, int changeLogPage)
        {
            var results = new List<(string brand, string baseUrl)>();
            using var ms = new MemoryStream(pdfBytes);
            using var document = PdfDocument.Open(ms, new ParsingOptions { ClipPaths = true });

            // Loop through pages until (changeLogPage - 1)
            for (int pageNumber = 1; pageNumber < changeLogPage; pageNumber++)
            {
                PageArea pageArea = ObjectExtractor.Extract(document, pageNumber);
                ExtractWithAlgorithm(new SpreadsheetExtractionAlgorithm(), pageArea, results);
            }

            return results;

            // Local helper to extract tables using a full-page algorithm.
            void ExtractWithAlgorithm(IExtractionAlgorithm alg, PageArea pageArea, List<(string brand, string baseUrl)> list)
            {
                var tables = alg.Extract(pageArea);
                foreach (var t in tables)
                {
                    ParseTable(t, list);
                }
            }

            // Parse Tabula.Table into retailer rows.
            void ParseTable(Tabula.Table table, List<(string brand, string baseUrl)> list)
            {
                string?[] retailer = new string[3];
                foreach (var row in table.Rows)
                {
                    // Build cell texts preserving order
                    var cells = row.Select(c => c?.GetText()?.Trim() ?? string.Empty).ToList();
                    if (cells.TrueForAll(string.IsNullOrEmpty)) continue; // skip blank row

                    // A valid row has 3 columns: brand, base URL, and CDR brand.
                    int columnIndex = 0;
                    for (int c = 0; c < cells.Count; c++)
                    {
                        var cell = cells[c].Trim();
                        if (string.IsNullOrWhiteSpace(cell)) continue;

                        if (columnIndex >= retailer.Length) continue; // skip if we already filled all columns

                        retailer[columnIndex] = cell;
                        columnIndex++;
                    }

                    if (columnIndex != retailer.Length) continue;

                    var url = retailer[1];
                    if (url is not null && url.StartsWith("https://cdr.energymadeeasy.gov.au/", StringComparison.OrdinalIgnoreCase))
                    {
                        var brand = retailer[2];
                        if (string.IsNullOrWhiteSpace(brand) || brand.Contains(' '))
                        {
                            throw new InvalidDataException($"CDR brand '{retailer[2]}' contains spaces is not valid.");
                        }
                        list.Add((brand, url));
                    }
                }
            }
        }
    }
}
