using System;
using KeyVault.Acmebot.Models;
using Microsoft.Extensions.Logging;

namespace KeyVault.Acmebot.Internal
{
    /// <summary>
    /// Service for evaluating ACME Renewal Information (ARI) renewal windows and timing calculations
    /// </summary>
    public class RenewalWindowService
    {
        private readonly ILogger<RenewalWindowService> _logger;

        public RenewalWindowService(ILogger<RenewalWindowService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Determines if the current time falls within the suggested renewal window
        /// </summary>
        /// <param name="renewalInfo">The renewal information from ARI</param>
        /// <returns>True if renewal should occur now, false otherwise</returns>
        public bool IsWithinRenewalWindow(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                _logger.LogWarning("Renewal info or suggested window is null");
                return false;
            }

            var now = DateTime.UtcNow;
            var window = renewalInfo.SuggestedWindow;

            var isWithin = now >= window.Start && now <= window.End;

            _logger.LogDebug("Renewal window evaluation: Current={CurrentTime}, Window={WindowStart} to {WindowEnd}, IsWithin={IsWithin}",
                now, window.Start, window.End, isWithin);

            if (isWithin)
            {
                _logger.LogInformation("Current time is within ARI renewal window. Renewal recommended.");
            }
            else if (now < window.Start)
            {
                _logger.LogInformation("Current time is before ARI renewal window. Time until window: {TimeUntil}",
                    window.Start - now);
            }
            else
            {
                _logger.LogWarning("Current time is past ARI renewal window end. Immediate renewal recommended.");
                return true; // Past the window end, should renew immediately
            }

            return isWithin;
        }

        /// <summary>
        /// Calculates the time remaining until the renewal window opens
        /// </summary>
        /// <param name="renewalInfo">The renewal information from ARI</param>
        /// <returns>Time until renewal window opens, or TimeSpan.Zero if already open or past</returns>
        public TimeSpan GetTimeUntilRenewalWindow(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                _logger.LogWarning("Renewal info or suggested window is null");
                return TimeSpan.Zero;
            }

            var now = DateTime.UtcNow;
            var windowStart = renewalInfo.SuggestedWindow.Start;

            if (now >= windowStart)
            {
                _logger.LogDebug("Renewal window has already started or passed");
                return TimeSpan.Zero;
            }

            var timeUntil = windowStart - now;
            
            _logger.LogDebug("Time until renewal window opens: {TimeUntil}", timeUntil);
            
            return timeUntil;
        }

        /// <summary>
        /// Calculates when to next check for renewal based on ARI recommendations
        /// </summary>
        /// <param name="renewalInfo">The renewal information from ARI</param>
        /// <returns>The next time to check for renewal</returns>
        public DateTime CalculateNextCheckTime(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                _logger.LogWarning("Renewal info or suggested window is null, using default check time");
                return DateTime.UtcNow.AddHours(24); // Default to check in 24 hours
            }

            var now = DateTime.UtcNow;
            var window = renewalInfo.SuggestedWindow;

            DateTime nextCheckTime;

            if (now < window.Start)
            {
                // Before renewal window - check at window start or partway through lead time
                var timeUntilWindow = window.Start - now;
                
                if (timeUntilWindow > TimeSpan.FromDays(7))
                {
                    // If more than a week away, check in a few days
                    nextCheckTime = now.AddDays(3);
                }
                else if (timeUntilWindow > TimeSpan.FromDays(1))
                {
                    // If more than a day away, check daily
                    nextCheckTime = now.AddDays(1);
                }
                else
                {
                    // Close to window, check at window start
                    nextCheckTime = window.Start;
                }
            }
            else if (now >= window.Start && now <= window.End)
            {
                // Within renewal window - should renew now, but if not, check again soon
                nextCheckTime = now.AddHours(6); // Check every 6 hours during window
            }
            else
            {
                // Past renewal window - urgent renewal needed
                nextCheckTime = now.AddMinutes(30); // Check very soon
            }

            _logger.LogDebug("Calculated next check time: {NextCheckTime} (in {TimeFromNow})",
                nextCheckTime, nextCheckTime - now);

            return nextCheckTime;
        }

        /// <summary>
        /// Determines the optimal time within the renewal window to perform renewal
        /// </summary>
        /// <param name="renewalInfo">The renewal information from ARI</param>
        /// <returns>The optimal renewal time</returns>
        public DateTime CalculateOptimalRenewalTime(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                _logger.LogWarning("Renewal info or suggested window is null, using current time");
                return DateTime.UtcNow;
            }

            var window = renewalInfo.SuggestedWindow;
            var now = DateTime.UtcNow;

            // If we're past the window, renew immediately
            if (now > window.End)
            {
                _logger.LogWarning("Past renewal window end, renewing immediately");
                return now;
            }

            // If we're before the window, use window start
            if (now < window.Start)
            {
                _logger.LogDebug("Before renewal window, optimal time is window start");
                return window.Start;
            }

            // We're within the window - calculate optimal time
            // Prefer to renew in the first third of the window for safety
            var windowDuration = window.End - window.Start;
            var optimalOffset = windowDuration.TotalMilliseconds * 0.33; // First third
            var optimalTime = window.Start.AddMilliseconds(optimalOffset);

            // But don't schedule in the past
            if (optimalTime < now)
            {
                optimalTime = now;
            }

            _logger.LogDebug("Optimal renewal time calculated: {OptimalTime}", optimalTime);
            return optimalTime;
        }

        /// <summary>
        /// Validates that a renewal window is properly formed and reasonable
        /// </summary>
        /// <param name="renewalInfo">The renewal information to validate</param>
        /// <returns>True if the renewal window is valid</returns>
        public bool IsValidRenewalWindow(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                _logger.LogWarning("Renewal info or suggested window is null");
                return false;
            }

            var window = renewalInfo.SuggestedWindow;

            // Check that start is before end
            if (window.Start >= window.End)
            {
                _logger.LogError("Invalid renewal window: start time {Start} is not before end time {End}",
                    window.Start, window.End);
                return false;
            }

            // Check that the window is not unreasonably long (more than 90 days)
            var windowDuration = window.End - window.Start;
            if (windowDuration > TimeSpan.FromDays(90))
            {
                _logger.LogWarning("Renewal window duration is very long: {Duration} days", windowDuration.TotalDays);
                // Don't fail validation, just log warning
            }

            // Check that the window is not unreasonably short (less than 1 hour)
            if (windowDuration < TimeSpan.FromHours(1))
            {
                _logger.LogWarning("Renewal window duration is very short: {Duration} minutes", windowDuration.TotalMinutes);
                // Don't fail validation, just log warning
            }

            // Check that the window is not too far in the past (more than 7 days)
            var now = DateTime.UtcNow;
            if (window.End < now.AddDays(-7))
            {
                _logger.LogError("Renewal window ended more than 7 days ago: {WindowEnd}", window.End);
                return false;
            }

            _logger.LogDebug("Renewal window validation passed: {Start} to {End} (duration: {Duration})",
                window.Start, window.End, windowDuration);

            return true;
        }

        /// <summary>
        /// Gets a human-readable description of the renewal window status
        /// </summary>
        /// <param name="renewalInfo">The renewal information from ARI</param>
        /// <returns>Status description string</returns>
        public string GetRenewalWindowStatus(RenewalInfoResponse renewalInfo)
        {
            if (renewalInfo?.SuggestedWindow == null)
            {
                return "No renewal window information available";
            }

            var now = DateTime.UtcNow;
            var window = renewalInfo.SuggestedWindow;

            if (now < window.Start)
            {
                var timeUntil = window.Start - now;
                return $"Renewal window opens in {FormatTimeSpan(timeUntil)}";
            }
            else if (now >= window.Start && now <= window.End)
            {
                var timeRemaining = window.End - now;
                return $"Within renewal window (closes in {FormatTimeSpan(timeRemaining)})";
            }
            else
            {
                var timePast = now - window.End;
                return $"Renewal window closed {FormatTimeSpan(timePast)} ago - urgent renewal needed";
            }
        }

        /// <summary>
        /// Formats a TimeSpan into a human-readable string
        /// </summary>
        private static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
            {
                return $"{timeSpan.TotalDays:F1} days";
            }
            else if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.TotalHours:F1} hours";
            }
            else
            {
                return $"{timeSpan.TotalMinutes:F0} minutes";
            }
        }
    }
}
