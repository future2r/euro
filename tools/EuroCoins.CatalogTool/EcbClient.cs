using System.Net;
using System.Net.Http.Headers;

namespace EuroCoins.CatalogTool;

/// <summary>
/// Downloads pages and images from the ECB (and flags from flagcdn.com) politely: identified
/// user agent, retries for transient errors, no retry for client errors such as 404.
/// </summary>
internal sealed class EcbClient : IDisposable
{
    /// <summary>The ECB web site.</summary>
    public static readonly Uri BaseUri = new("https://www.ecb.europa.eu/");

    private const int Attempts = 3;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(40) };

    public EcbClient()
    {
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EuroCoins-CatalogTool", "1.0"));
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(non-commercial catalog of euro coins)"));
    }

    /// <summary>Returns the page text, or <see langword="null"/> if the page does not exist (yet).</summary>
    public async Task<string?> GetPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        var bytes = await GetFileAsync(uri, cancellationToken);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    /// <summary>Returns the file content, or <see langword="null"/> for a client error (4xx), e.g. a page that does not exist yet.</summary>
    public async Task<byte[]?> GetFileAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(uri, cancellationToken);
                if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
                {
                    return null; // e.g. the page of a future year
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < Attempts && ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    public void Dispose() => http.Dispose();
}
