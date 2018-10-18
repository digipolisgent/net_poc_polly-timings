using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Sync
{
    public class Demo10_Wrap_Timeout : SyncDemo
    {
        private int _totalRequests;
        private int _eventualSuccesses;
        private int _retries;
        private int _eventualFailuresDueToTimeout;
        private int _eventualFailuresForOtherReasons;

        public override string Description => "Demonstrates timeout policy on a call that always takes to long. Retry 3 times. Green message will never appear because it's broken by the wait and retry";

        public override void Execute(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            Init(progress);

            var timeoutPolicy = Policy.Timeout(TimeSpan.FromSeconds(2), TimeoutStrategy.Pessimistic, (context, span, task) =>
            {
                if (task.Status == TaskStatus.Running)
                    progress.Report(ProgressWithMessage($"Timeout policy called: call exceeded {span.TotalSeconds}s", Color.Magenta));
            });

            RetryPolicy waitAndRetryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(4), // Wait 4s between each try.
                    onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
                    {
                        // This is your new exception handler! 
                        // Tell the user what they've won!
                        progress.Report(ProgressWithMessage("Retry is starting.... Logging: " + exception.Message, Color.Yellow));
                    });

            FallbackPolicy<string> fallbackForAnyException = Policy<string>
                .Handle<Exception>()
                .Fallback(
                    fallbackAction: () => "Please try again later [Fallback for any exception]",
                    onFallback: e =>
                    {
                        progress.Report(ProgressWithMessage("Fallback catches eventually failed with: " + e.Exception.Message, Color.Red));
                    }
                );

            // Wrap the policies (timeout check on every request)
            var policyWrap = fallbackForAnyException.Wrap(waitAndRetryPolicy).Wrap(timeoutPolicy);

            using (var client = new WebClient())
            {
                _totalRequests = 0;
                try
                {
                    // Manage the call according to the whole policy wrap.
                    progress.Report(ProgressWithMessage("Start polywrap"));
                    string response = policyWrap.Execute(ct => client.DownloadString(Configuration.WEB_API_ROOT + "/api/speed/veryslow"), cancellationToken);
                    progress.Report(ProgressWithMessage("Response: " + response, Color.Green));
                    progress.Report(ProgressWithMessage("==============================================="));

                    _eventualSuccesses++;
                }
                catch (Exception e) // try-catch not needed, now that we have a Fallback.Handle<Exception>.  It's only been left in to *demonstrate* it should never get hit.
                {
                    throw new InvalidOperationException("Should never arrive here.  Use of fallbackForAnyException should have provided nice fallback value for any exceptions.", e);
                }
            }
        }

        public override Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", _totalRequests),
            new Statistic("Requests which eventually succeeded", _eventualSuccesses, Color.Green),
            new Statistic("Retries made to help achieve success", _retries, Color.Yellow),
            new Statistic("Requests timed out by timeout policy", _eventualFailuresDueToTimeout, Color.Magenta),
            new Statistic("Requests which failed after longer delay", _eventualFailuresForOtherReasons, Color.Red),
        };

        private void Init(IProgress<DemoProgress> progress)
        {
            _totalRequests = 0;
            _eventualSuccesses = 0;
            _retries = 0;
            _eventualFailuresDueToTimeout = 0;
            _eventualFailuresForOtherReasons = 0;

            progress.Report(ProgressWithMessage(typeof(Demo10_Wrap_Timeout).Name));
            progress.Report(ProgressWithMessage("======"));
            progress.Report(ProgressWithMessage(string.Empty));
        }
    }
}
