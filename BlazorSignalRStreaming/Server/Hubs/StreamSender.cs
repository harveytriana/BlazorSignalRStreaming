// ===============================
// Blazor Spread
// ===============================
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BlazorSignalRStreaming.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorSignalRStreaming.Server.Hubs
{
    // SERVER-TO-CLIENT STREAMING
    public class StreamSender : Hub
    {
        readonly ILogger<StreamSender> _logger;
        readonly int _delay = 300;

        public StreamSender(ILogger<StreamSender> logger)
        {
            _logger = logger;
        }

        // First apprach. Return an IAsyncEnumerable<T> 
        public async IAsyncEnumerable<WeatherForecast> Send(
            int count,
            [EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Run IAsyncEnumerable<T> CounterEnumerable(count: {count})");

            for (int i = 1; i <= count; i++) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                // send to client
                yield return WeatherForecast.Create(i); 

                // simulation
                await Task.Delay(_delay, cancellationToken);

                _logger.LogInformation($"dispatched: {i}");
            }
            _logger.LogInformation($"End of stream");
        }

        // Second apprach. Return an IAsyncEnumerable<T> 
        public ChannelReader<WeatherForecast> SendChannel(
            int count,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Run ChannelReader<iT> CounterChannel(count: {count})");

            var channel = Channel.CreateUnbounded<WeatherForecast>();

            // We don't want to await WriteItemsAsync, otherwise we'd end up waiting 
            // for all the items to be written before returning the channel back to
            // the client.
            //- _ = WriteItemsAsync(channel.Writer, count, delay, cancellationToken);

            Exception localException = null;

            _ = Task.Run(async () => {
                try {
                    for (var i = 0; i < count; i++) {
                        // Use the cancellationToken in other APIs that accept cancellation
                        // tokens so the cancellation can flow down to them.
                        // cancellationToken.ThrowIfCancellationRequested();

                        // ChannelWriter sends data to the client
                        await channel.Writer.WriteAsync(WeatherForecast.Create(i), cancellationToken);

                        // simulation
                        await Task.Delay(_delay, cancellationToken);
                    }
                    channel.Writer.Complete();
                }
                catch (Exception exception) {
                    localException = exception;
                }
                finally {
                    channel.Writer.Complete(localException);
                }
            }, cancellationToken);
            return channel.Reader;
        }
    }
}