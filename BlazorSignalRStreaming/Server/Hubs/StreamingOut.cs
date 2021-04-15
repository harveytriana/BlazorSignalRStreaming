// ===============================
// Blazor Spread
// ===============================
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BlazorSignalRStreaming.Server.Hubs
{
    // CLIENT-TO-SERVER STREAMING
    public class StreamingOut : Hub
    {
        private readonly ILogger<StreamingOut> _logger;

        public StreamingOut(ILogger<StreamingOut> logger)
        {
            _logger = logger;
        }

        // The client receiving Streams on the Server

        // first approach, ChannelReader<T>
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

        // Second apporach. IAsyncEnumerable<T>
        //! requires C# 8.0 or later.
        public async Task UploadStreamEnumerable(IAsyncEnumerable<string> stream)
        {
            _logger.LogInformation($"UploadStreamEnumerable(IAsyncEnumerable stream: {stream})", true);

            await foreach (var item in stream) {
                // do something with the stream item
                _logger.LogInformation($"From client: {item}", true);
            }
        }

        
    }
}