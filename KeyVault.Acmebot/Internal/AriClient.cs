using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using KeyVault.Acmebot.Models;
using KeyVault.Acmebot.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// HTTP client for ACME Renewal Information (ARI) endpoints
    /// </summary>
    public class AriClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AriClient> _logger;
        private readonly AcmebotOptions _options;

        public AriClient(HttpClient httpClient, ILogger<AriClient> logger, IOptions<AcmebotOptions> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            // Configure HTTP client for ARI requests
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"KeyVault-Acmebot/{Constants.ApplicationVersion}");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets renewal information for a certificate from the ARI endpoint
        /// </summary>
        /// <param name="ariUrl">The complete ARI URL for the certificate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Renewal info response or null if not available</returns>
        public async Task<RenewalInfoResponse> GetRenewalInfoAsync(string ariUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(ariUrl))
            {
                _logger.LogWarning("ARI URL is null or empty");
                return null;
            }

            var retryCount = 0;
            var maxRetries = _options.AriMaxRetries;

            while (retryCount <= maxRetries)
            {
                try
                {
                    _logger.LogDebug("Requesting renewal info from ARI endpoint: {AriUrl} (attempt {Attempt}/{MaxRetries})", 
                        ariUrl, retryCount + 1, maxRetries + 1);

                    using var response = await _httpClient.GetAsync(ariUrl, cancellationToken);

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            var renewalInfo = await response.Content.ReadFromJsonAsync<RenewalInfoResponse>(cancellationToken: cancellationToken);
                            
                            if (renewalInfo?.SuggestedWindow != null)
                            {
                                _logger.LogInformation("Successfully retrieved ARI data. Renewal window: {Start} to {End}",
                                    renewalInfo.SuggestedWindow.Start, renewalInfo.SuggestedWindow.End);
                                return renewalInfo;
                            }
                            
                            _logger.LogWarning("ARI response missing required SuggestedWindow data");
                            return null;

                        case HttpStatusCode.NotFound:
                            _logger.LogInformation("Certificate not found in ARI system (404). This is normal for new certificates.");
                            return null;

                        case HttpStatusCode.TooManyRequests:
                            if (!_options.AriRespectRateLimits)
                            {
                                _logger.LogWarning("Rate limit hit but AriRespectRateLimits is disabled. Aborting ARI request.");
                                return null;
                            }

                            var retryAfter = await HandleRateLimitingAsync(response);
                            if (retryAfter.HasValue && retryCount < maxRetries)
                            {
                                _logger.LogInformation("Rate limited. Waiting {Seconds} seconds before retry", retryAfter.Value.TotalSeconds);
                                await Task.Delay(retryAfter.Value, cancellationToken);
                                retryCount++;
                                continue;
                            }
                            
                            _logger.LogWarning("Rate limit exceeded and no retry possible");
                            return null;

                        case HttpStatusCode.BadRequest:
                            _logger.LogError("Bad request to ARI endpoint. Certificate ID may be invalid: {AriUrl}", ariUrl);
                            return null;

                        case HttpStatusCode.InternalServerError:
                        case HttpStatusCode.BadGateway:
                        case HttpStatusCode.ServiceUnavailable:
                        case HttpStatusCode.GatewayTimeout:
                            if (retryCount < maxRetries)
                            {
                                var delay = CalculateExponentialBackoff(retryCount);
                                _logger.LogWarning("Server error {StatusCode}. Retrying in {Delay} seconds", 
                                    response.StatusCode, delay.TotalSeconds);
                                await Task.Delay(delay, cancellationToken);
                                retryCount++;
                                continue;
                            }
                            
                            _logger.LogError("Server error {StatusCode} after {MaxRetries} retries", 
                                response.StatusCode, maxRetries);
                            return null;

                        default:
                            _logger.LogError("Unexpected HTTP status code from ARI endpoint: {StatusCode}", response.StatusCode);
                            return null;
                    }
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    if (retryCount < maxRetries)
                    {
                        var delay = CalculateExponentialBackoff(retryCount);
                        _logger.LogWarning("Request timeout. Retrying in {Delay} seconds", delay.TotalSeconds);
                        await Task.Delay(delay, cancellationToken);
                        retryCount++;
                        continue;
                    }
                    
                    _logger.LogError("Request timeout after {MaxRetries} retries", maxRetries);
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    if (retryCount < maxRetries)
                    {
                        var delay = CalculateExponentialBackoff(retryCount);
                        _logger.LogWarning(ex, "HTTP request exception. Retrying in {Delay} seconds", delay.TotalSeconds);
                        await Task.Delay(delay, cancellationToken);
                        retryCount++;
                        continue;
                    }
                    
                    _logger.LogError(ex, "HTTP request failed after {MaxRetries} retries", maxRetries);
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during ARI request to {AriUrl}", ariUrl);
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles rate limiting by parsing Retry-After header
        /// </summary>
        /// <param name="response">HTTP response with rate limit status</param>
        /// <returns>Delay before retry, or null if no retry should be attempted</returns>
        private async Task<TimeSpan?> HandleRateLimitingAsync(HttpResponseMessage response)
        {
            try
            {
                // Check for Retry-After header
                if (response.Headers.RetryAfter != null)
                {
                    if (response.Headers.RetryAfter.Delta.HasValue)
                    {
                        return response.Headers.RetryAfter.Delta.Value;
                    }
                    
                    if (response.Headers.RetryAfter.Date.HasValue)
                    {
                        var retryTime = response.Headers.RetryAfter.Date.Value;
                        var delay = retryTime - DateTimeOffset.UtcNow;
                        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(60); // Default to 1 minute
                    }
                }

                // Try to parse error response for additional rate limit info
                var errorContent = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(errorContent))
                {
                    try
                    {
                        var errorResponse = System.Text.Json.JsonSerializer.Deserialize<AriErrorResponse>(errorContent);
                        if (errorResponse != null)
                        {
                            _logger.LogInformation("ARI rate limit error: {Type} - {Detail}", 
                                errorResponse.Type, errorResponse.Detail);
                        }
                    }
                    catch
                    {
                        // Ignore JSON parsing errors for error response
                    }
                }

                // Default retry delay for rate limiting
                return TimeSpan.FromMinutes(1);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling rate limiting response");
                return TimeSpan.FromMinutes(1);
            }
        }

        /// <summary>
        /// Calculates exponential backoff delay for retries
        /// </summary>
        /// <param name="retryAttempt">Current retry attempt (0-based)</param>
        /// <returns>Delay before next retry</returns>
        private static TimeSpan CalculateExponentialBackoff(int retryAttempt)
        {
            // Exponential backoff: 2^attempt seconds, capped at 5 minutes
            var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
            var maxDelay = TimeSpan.FromMinutes(5);
            
            return delay > maxDelay ? maxDelay : delay;
        }

        /// <summary>
        /// Validates that an ARI URL is properly formatted
        /// </summary>
        /// <param name="ariUrl">The ARI URL to validate</param>
        /// <returns>True if the URL appears valid</returns>
        public static bool IsValidAriUrl(string ariUrl)
        {
            if (string.IsNullOrEmpty(ariUrl))
            {
                return false;
            }

            try
            {
                var uri = new Uri(ariUrl);
                return uri.Scheme == "https" &&
                       !string.IsNullOrEmpty(uri.Host);
            }
            catch
            {
                return false;
            }
        }
    }
}
