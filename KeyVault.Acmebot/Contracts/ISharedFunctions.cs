using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ACMESharp.Protocol;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Models;

namespace KeyVault.Acmebot.Contracts
{
    public interface ISharedFunctions
    {
        Task<IList<CertificateItem>> GetExpiringCertificates(DateTime currentDateTime);

        Task<IList<CertificateItem>> GetAllCertificates(object input = null);

        Task<IList<string>> GetZones(object input = null);

        Task<OrderDetails> Order(string[] dnsNames);

        Task Dns01Precondition(string[] dnsNames);

        Task<IList<AcmeChallengeResult>> Dns01Authorization(string[] authorizationUrls);

        [RetryOptions("00:00:10", 12, HandlerType = typeof(RetryStrategy), HandlerMethodName = nameof(RetryStrategy.RetriableException))]
        Task CheckDnsChallenge(IList<AcmeChallengeResult> challengeResults);

        Task AnswerChallenges(IList<AcmeChallengeResult> challengeResults);

        [RetryOptions("00:00:05", 12, HandlerType = typeof(RetryStrategy), HandlerMethodName = nameof(RetryStrategy.RetriableException))]
        Task CheckIsReady((OrderDetails, IList<AcmeChallengeResult>) input);

        Task<CertificateItem> FinalizeOrder((string[], OrderDetails) input);

        Task CleanupDnsChallenge(IList<AcmeChallengeResult> challengeResults);

        Task SendCompletedEvent((string, DateTimeOffset?, string[]) input);
    }
}
