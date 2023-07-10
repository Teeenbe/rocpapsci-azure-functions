using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace RockPaperScissorsFunctions
{
    public class RpsRecordResults
    {
        [FunctionName("RpsRecordResults")]
        public void Run(
            [ServiceBusTrigger("rockpaperscissorsgameresults", Connection = "RpsBusConnection")]
            string myQueueItem,
            ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
        }
    }
}
