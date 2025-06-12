using System;
using System.Text.Json.Serialization;

namespace KeyVault.Acmebot.Models;

/// <summary>
/// Represents the response from an ACME Renewal Information (ARI) endpoint
/// </summary>
public class RenewalInfoResponse
{
    /// <summary>
    /// The suggested time window for certificate renewal
    /// </summary>
    [JsonPropertyName("suggestedWindow")]
    public SuggestedWindow SuggestedWindow { get; set; }

    /// <summary>
    /// Optional URL providing human-readable explanation of renewal timing
    /// </summary>
    [JsonPropertyName("explanationURL")]
    public string ExplanationUrl { get; set; }
}

/// <summary>
/// Represents the suggested renewal window from ARI
/// </summary>
public class SuggestedWindow
{
    /// <summary>
    /// Start time of the suggested renewal window (RFC 3339 format)
    /// </summary>
    [JsonPropertyName("start")]
    public DateTime Start { get; set; }

    /// <summary>
    /// End time of the suggested renewal window (RFC 3339 format)
    /// </summary>
    [JsonPropertyName("end")]
    public DateTime End { get; set; }
}
