using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ACMESharp.Protocol;

using DurableTask.TypedProxy;

using KeyVault.Acmebot.Models;

using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.Dns.Models;

namespace KeyVault.Acmebot.Contracts
{
    public interface ISharedFunctions
    {
        Task<IList<CertificateBundle>> GetCertificates(DateTime currentDateTime);

        Task<IList<Zone>> GetZones(object input = null);

        Task<OrderDetails> Order(string[] hostNames);

        Task Dns01Precondition(string[] hostNames);

        Task<IList<AcmeChallengeResult>> Dns01Authorization(string[] authorizationUrls);

        [RetryOptions("00:00:10", 6, HandlerType = typeof(RetryStrategy), HandlerMethodName = nameof(RetryStrategy.RetriableException))]
        Task CheckDnsChallenge(IList<AcmeChallengeResult> challengeResults);

        [RetryOptions("00:00:05", 12, HandlerType = typeof(RetryStrategy), HandlerMethodName = nameof(RetryStrategy.RetriableException))]
        Task CheckIsReady(OrderDetails orderDetails);

        Task AnswerChallenges(IList<AcmeChallengeResult> challengeResults);

        Task FinalizeOrder((string[], OrderDetails) input);
    }
}