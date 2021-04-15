// ===============================
// Blazor Spread
// ===============================
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorSignalRStreaming.Server.Hubs
{
    // SERVER-TO-CLIENT STREAMING
    public class StreamingIn : Hub
    {
        private readonly ILogger<StreamingIn> _logger;

        public StreamingIn(ILogger<StreamingIn> logger)
        {
            _logger = logger;
        }

        // hub method becomes a Streaming hub method when it returns IAsyncEnumerable<T>, ChannelReader<T>
        // or async versions
        // first approach, ChannelReader<T>
        //
        // NOTE
        // 2020 - can? public async Task<ChannelReader<int>> CounterChannel( 
        // NOT (Exception thrown: System.Reflection.TargetInvocationException)
        //
        public ChannelReader<int> CounterChannel(
                   int count,
                   int delay,
                   CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Run ChannelReader<int> CounterChannel(count: {count}, delay: {delay})");

            var channel = Channel.CreateUnbounded<int>();

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
                        await channel.Writer.WriteAsync(i, cancellationToken);

                        await Task.Delay(delay, cancellationToken);
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

        // Second apprach. IAsyncEnumerable<T> ... Server Application using Async Streams
        //! requires C# 8.0 or later.
        public async IAsyncEnumerable<int> CounterEnumerable(
            int count,
            int delay,
            [EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Run IAsyncEnumerable<int> CounterEnumerable(count: {count}, delay: {delay})");

            for (int i = 0; i < count; i++) {
                // 
                // Check the cancellation token regularly so that the server will stop
                // producing items if the client disconnects.
                cancellationToken.ThrowIfCancellationRequested();

                yield return i; // T instance;

                // Use the cancellationToken in other APIs that accept cancellation
                // tokens so the cancellation can flow down to them.
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}