// ===============================
// Blazor Spread
// ===============================
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorSignalRStreaming.Server.Hubs
{
    // CLIENT-TO-SERVER STREAMING
    public class StreamReceiver : Hub
    {
        private readonly ILogger<StreamReceiver> _logger;

        public StreamReceiver(ILogger<StreamReceiver> logger)
        {
            _logger = logger;
        }

        // First apporach. IAsyncEnumerable<T>
        public async Task UploadStream(IAsyncEnumerable<string> stream)
        {
            _logger.LogInformation($"UploadStream(IAsyncEnumerable stream: {stream})", true);

            await foreach (var item in stream) {
                // do something with the stream item
                _logger.LogInformation($"From client: {item}", true);
            }
        }

        // Second approach, ChannelReader<T>
        public async Task UploadStreamChannel(ChannelReader<string> stream)
        {
            _logger.LogInformation($"Run UploadStreamChannel(ChannelReader stream: {stream})", true);

            while (await stream.WaitToReadAsync()) {
                while (stream.TryRead(out var item)) {
                    // do something with the stream item
                    _logger.LogInformation($"From client: {item}", true);
                }
            }
        }
    }
}