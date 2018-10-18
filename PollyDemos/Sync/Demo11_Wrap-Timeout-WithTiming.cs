using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using Polly.Wrap;
using PollyDemos.OutputHelpers;

namespace PollyDemos.Sync
{
    public class Demo11_Wrap_Timeout_WithTiming : SyncDemo
    {
        private const int TimeoutFallback = 4; // force timeout when call takes more than x seconds
        private const int SlowRequestWarning = 2; // show a message when the call takes more than x seconds

        private IProgress<DemoProgress> _progress;
        private CancellationToken _cancelToken;

        private int _totalRequests;
        private int _eventualSuccesses;
        private int _retries;
        private int _eventualFailuresDueToTimeout;
        private int _eventualFailuresForOtherReasons;

        public override string Description => "Demonstrates slow call warning, timeout when call takes more than 10s";

        public override Statistic[] LatestStatistics => new[]
        {
            new Statistic("Total requests made", _totalRequests),
            new Statistic("Requests which eventually succeeded", _eventualSuccesses, Color.Green),
            new Statistic("Retries made to help achieve success", _retries, Color.Yellow),
            new Statistic("Requests timed out by timeout policy", _eventualFailuresDueToTimeout, Color.Magenta),
            new Statistic("Requests which failed after longer delay", _eventualFailuresForOtherReasons, Color.Red),
        };

        public override void Execute(CancellationToken cancellationToken, IProgress<DemoProgress> progress)
        {
            _progress = progress;
            _cancelToken = cancellationToken;

            Init();

            using (var client = new WebClient())
            {
                bool internalCancel = false;
                _totalRequests = 0;

                while (!internalCancel && !cancellationToken.IsCancellationRequested && !client.IsBusy)
                {
                    Request(client);
                    _progress.Report(ProgressWithMessage("========== CALL DONE =========="));
                    internalCancel = TerminateDemosByKeyPress && Console.KeyAvailable;
                }
            }
        }

        /// <summary>
        /// This method can be more generic, depending on the usage and response type
        /// </summary>
        private void Request(WebClient client)
        {
            try
            {
                // Manage the call according to the whole policy wrap.
                var policyWrap = BuildPolicy();
                string response = policyWrap.Execute(ct =>
                {
                    return TimeAndWarn(() => client.DownloadString(Configuration.WEB_API_ROOT + "/api/speed/various"));
                }, _cancelToken);

                _progress.Report(ProgressWithMessage($"Response: {response}", Color.Green));
                _eventualSuccesses++;
            }
            catch (Exception e) // try-catch not needed, now that we have a Fallback.Handle<Exception>.  It's only been left in to *demonstrate* it should never get hit.
            {
                throw new InvalidOperationException("Should never arrive here.  Use of fallbackForAnyException should have provided nice fallback value for any exceptions.", e);
            }
        }

        /// <summary>
        /// Time an action and validate
        /// </summary>
        private T TimeAndWarn<T>(Func<T> action)
        {
            var watch = new Stopwatch();
            watch.Start();

            // start timer
            var actionResult = action();

            if(watch.IsRunning)
                watch.Stop();

            if (watch.ElapsedMilliseconds > TimeSpan.FromSeconds(SlowRequestWarning).TotalMilliseconds)
                _progress.Report(ProgressWithMessage($"Slow call detected: { watch.ElapsedMilliseconds}", Color.Yellow));

            return actionResult;
        }

        private PolicyWrap<string> BuildPolicy()
        {
            // max timeout policy, to stop the entire request because of the API is being TOOO slow
            var timeoutPolicy = Policy.Timeout(TimeSpan.FromSeconds(TimeoutFallback), TimeoutStrategy.Pessimistic, (context, span, task) =>
            {
                if (task.Status == TaskStatus.Running)
                    _progress.Report(ProgressWithMessage($"Timeout policy called: call exceeded {span.TotalSeconds}s", Color.Magenta));
            });

            // fallback policy, what to do when a timeout is occured
            FallbackPolicy<string> fallbackForAnyException = Policy<string>
                .Handle<Exception>()
                .Fallback(
                    fallbackAction: () => "Please try again later [Fallback for any exception]",
                    onFallback: e =>
                    {
                        _progress.Report(ProgressWithMessage("Fallback catches eventually failed with: " + e.Exception.Message, Color.Red));
                    }
                );

            // wait and retry policy, when a call fails
            RetryPolicy waitAndRetryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(4), // Wait 4s between each try.
                    onRetry: (exception, calculatedWaitDuration) => // Capture some info for logging!
                    {
                        // This is your new exception handler! 
                        // Tell the user what they've won!
                        _progress.Report(ProgressWithMessage("Retry is starting.... Logging: " + exception.Message, Color.Yellow));
                    });

            // Wrap the policies together (timeout check on every request)
            return fallbackForAnyException.Wrap(waitAndRetryPolicy).Wrap(timeoutPolicy);
        }

        private void Init()
        {
            _totalRequests = 0;
            _eventualSuccesses = 0;
            _retries = 0;
            _eventualFailuresDueToTimeout = 0;
            _eventualFailuresForOtherReasons = 0;

            _progress.Report(ProgressWithMessage(typeof(Demo10_Wrap_Timeout).Name));
            _progress.Report(ProgressWithMessage("======"));
            _progress.Report(ProgressWithMessage(string.Empty));
        }
    }
}
