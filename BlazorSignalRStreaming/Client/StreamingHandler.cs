// ===============================
// Blazor Spread
// ===============================
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BlazorSignalRStreaming.Client
{
    class StreamingHandler : IDisposable
    {
        HubConnection _connection;

        readonly CancellationTokenSource _cts = new();

        bool _connected;

        public delegate void PromptEventHandler(string message);
        public event PromptEventHandler Prompt;

        public async Task<bool> ConnectAsync(string hubUrl)
        {
            try {
                _connection = new HubConnectionBuilder()
                    .WithUrl(hubUrl)
                    .Build();

                await _connection.StartAsync(_cts.Token);

                Prompt?.Invoke($"Hub is Started. Waiting Signals.");

                _connected = true;
            }
            catch (Exception exception) {
                Prompt?.Invoke($"Exception: {exception.Message}");
                _connected = false;
            }

            return _connected;
        }

        #region Server to Client
        // the data is streamed from the server to the client.
        // Hub's method note:
        // CounterChannel: ChannelReader<T>
        // CounterEnumerable: IAsyncEnumerable<T>
        // Both are valid for these methods:
        //
        // Works with CounterChannel, but 
        // works with CounterEnumerable too
        public async Task ReadStreamChannel()
        {
            if (!_connected) {
                return;
            }
            var channel = await _connection.StreamAsChannelAsync<int>("CounterEnumerable", 12, 333, _cts.Token);
            // Wait asynchronously for data to become available
            while (await channel.WaitToReadAsync()) {
                // Read all currently available data synchronously, before waiting for more data
                while (channel.TryRead(out int data)) {
                    Prompt?.Invoke($"Received {data}");
                }
            }
            Prompt?.Invoke("COMPLETED");
        }

        // StreamAsync<T>
        // valid for C# 8 -> .NET Core 3.0+
        // testing in console app ... new StreamingTest().StreamingAsync().Wait();
        public async Task StartDownlodStream()
        {
            // The correct syntax is:
            await foreach (var count in _connection.StreamAsync<int>("CounterEnumerable", 12, 333, _cts.Token)) {
                Prompt?.Invoke($"Received {count}");
            }
            Prompt?.Invoke("Completed");
        }

        // NOTES
        //  It seemed to happen, sometimes
        //  ISSUE. When close the Window
        //? Exception thrown: 'System.IO.IOException' in System.Net.Sockets.dll
        //
        //? await? 'IAsyncEnumerable<int>' does not contain a definition for 'GetAwaiter'
        //  https://github.com/dotnet/AspNetCore.Docs/issues/20562
        //
        async Task ReadStream_0()
        {
            // await? 'IAsyncEnumerable<int>' does not contain a definition for 'GetAwaiter'
            var stream = _connection.StreamAsync<int>("CounterEnumerable", 12, 333, _cts.Token);

            // The correct syntax is:
            await foreach (var count in stream) {
                Prompt?.Invoke($"Received {count}");
            }
            Prompt?.Invoke("Completed");
        }
        #endregion

        #region Client to Server
        public async Task SendStreamBasicDemotration()
        {
            if (!_connected) {
                return;
            }
            Prompt?.Invoke("SendStreamBasicDemotration");

            var channel = Channel.CreateBounded<string>(10);
            await _connection.SendAsync("UploadStreamChannel", channel.Reader);
            await channel.Writer.WriteAsync("some data");
            await channel.Writer.WriteAsync("some more data");
            channel.Writer.Complete();

            Prompt?.Invoke("Completed");
        }

        public async Task SendStreamChannel()
        {
            if (!_connected) {
                return;
            }
            Prompt?.Invoke("SendStreamChannel");

            var channel = Channel.CreateBounded<string>(10);
            await _connection.SendAsync("UploadStreamChannel", channel.Reader);

            for (int i = 1; i < 8; i++) {
                var s = $"Some data {i}";
                Prompt?.Invoke($"Sending -> {s}");

                await channel.Writer.WriteAsync(s);
                await Task.Delay(333);
            }

            channel.Writer.Complete();
            Prompt?.Invoke("Completed");
        }

        public async Task SendStreamEnumerable()
        {
            Prompt?.Invoke("SendStreamEnumerable");
            await _connection.SendAsync("UploadStreamEnumerable", ClientStreamData());
        }

        async IAsyncEnumerable<string> ClientStreamData()
        {
            for (var i = 0; i < 8; i++) {
                var s = $"Some data {i}";
                Prompt?.Invoke($"Sending -> {s}");
                await Task.Delay(333);
                yield return s;
            }
            Prompt?.Invoke("Completed");
        }
        #endregion

        public void Cancel()
        {
            _cts.Cancel();
            Prompt?.Invoke("CANCEL");
        }

        public void Dispose()
        {
            if (_connected) {
                _connection.StopAsync();
                _connection.DisposeAsync();
            }
        }
    }
}
