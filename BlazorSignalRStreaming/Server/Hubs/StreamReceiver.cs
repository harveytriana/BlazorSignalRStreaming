// ===============================
// Blazor Spread
// ===============================
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using BlazorSignalRStreaming.Shared;
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
        public async Task UploadStream(IAsyncEnumerable<WeatherForecast> clientStream)
        {
            _logger.LogInformation($"UploadStream(IAsyncEnumerable stream: {clientStream})", true);

            await foreach (var item in clientStream) {
                // do something with the incomming item...
                _logger.LogInformation($"From client: {item}");
            }
        }

        // Second approach, ChannelReader<T>
        public async Task UploadStreamChannel(ChannelReader<WeatherForecast> clientStream)
        {
            _logger.LogInformation($"Run UploadStreamChannel(ChannelReader stream: {clientStream})", true);

            while (await clientStream.WaitToReadAsync()) {
                while (clientStream.TryRead(out var item)) {
                    // do something with the incomming item...
                    _logger.LogInformation($"From client: {item}", true);
                }
            }
        }
    }
}