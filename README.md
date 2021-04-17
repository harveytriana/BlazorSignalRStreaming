# Using SignalR Streaming in Blazor

---

*Real-time data where partial are sent or received without waiting for a single transfer of the expected data.*

A certainly advanced feature that SignalR has is the transmission of point-to-point data in chunks, a strategy technically known as `Streaming`. This scenario is ideal when we are going to transfer a considerable volume of objects in real time, either from the server or from the client, and we do not want to wait until the entire task is finished to do something with the data.

SignalR supports transfer from client to server and from server to client. There are two techniques, the first and oldest is using `ChannelReader`,  the second, born with C# 8, is asynchronous transmission from `Yield`. The second technique is more convenient and less complex to deal with, and it is the one I will discuss in this article. However, in the Git source, I leave the counterpart with `ChannelReader`. For the consumer it is the same to use one another, however, for the server the improvement in code that the asynchronous `Yield` gives us is notable.

## The example

The example refers to a Blazor application hosted on ASP.NET Core, named `BlazorSignalRStreaming`, which shows the two scenarios: (1) Outbound Streaming, (2) Inbound Streaming. As an object model I took the classic `WeatherForecast` with a slight modification as read here:

*The model*

```csharp
using System;

namespace BlazorSignalRStreaming.Shared
{
    public class WeatherForecast
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int TemperatureC { get; set; }
        public string Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        #region Random Instance
        static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild",
            "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        static readonly Random random = new();

        public static WeatherForecast Create(int id)
        {
            return new WeatherForecast {
                Id = id,
                Date = DateTime.Today.AddMinutes(random.Next(-10, 10)),
                TemperatureC = random.Next(-20, 55),
                Summary = Summaries[random.Next(Summaries.Length)]
            };
        }
        #endregion

        public override string ToString()
        {
            return $"{Id} {Date.ToShortDateString()} {TemperatureC:N2} {Summary}";
        }
    }
}
```

## Data transfer from Server to Client

Case in which the client makes a request to the server for it to send it a list of objects of the same type, so that these objects enter one by one or in batches, and can be processed on the client as soon as they arrive. A practical example could be that we request data from a continuous graph and without waiting for the entire matrix to arrive, we show the corresponding information in the user interface. In this way, the impact on the transmission is light and effective, and the appropriate interface is available.

The SignalR hub consists of a class that derives from `Hub`,  and contains a function that returns `IAsyncEnumerable<T>` asynchronous. From the example:

```csharp
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
    }
}
```

> The forced wait with the `delay` variable is simply for illustration of behavior.

**The Client**

The Blazor client must contain a reference to `Microsoft.AspNetCore.SignalR.Client`. A component that requests the SignalR transmission needs a code like the following,

*Blazor StreamIn Component*

```csharp
@using Microsoft.AspNetCore.SignalR.Client
@using System.Threading
@using BlazorSignalRStreaming.Shared
@page "/streaming-in"
@inject NavigationManager NavigationManager
@implements IAsyncDisposable

<h3><i class="oi oi-cloud-download"></i> SignalR Streaming In</h3>
<hr />
<h5>
    The client receives a stream of the Server.
</h5>
<br />
<div>
    <button class="btn btn-primary"
            style="width:130px;"
            disabled="@(state=="CANCEL")"
            @onclick="Start">
        Start
    </button>
    <button class="btn btn-danger"
            style="width:130px;"
            disabled="@(state=="START")"
            @onclick="Cancel">
        Cancel
    </button>
</div>
<hr />
<ul style="font-family:Consolas">
    @foreach (var i in ls) {
        <li>@i</li>
    }
</ul>
<p>
    @status
</p>

@code{
    HubConnection connection;
    CancellationTokenSource cts;
    bool connected;
    string state = "START";
    string status;
    List<string> ls = new();
    int n = 20;

    async Task Start()
    {
        state = "CANCEL";
        status = "";
        ls.Clear();

        await ConnectAsync();
        if (connected) {
            await StartDownlodStream();
        }
    }

    public async Task StartDownlodStream()
    {
        var remoteStream = connection.StreamAsync<WeatherForecast>("Send", n, cts.Token);
        //
        await foreach (var i in remoteStream) {
            ls.Add($"Received: {i}");
            // do something with instance i...
            StateHasChanged();
        }
        status = "Completed";
        state = "START";
        await DisposeAsync();
    }

    async Task Cancel()
    {
        cts.CancelAfter(300);
        status = "Canceled.";
        state = "START";
        await Task.Delay(300);
    }

    async Task ConnectAsync()
    {
        connected = false;

        var hubUrl = NavigationManager.ToAbsoluteUri("/StreamSender").ToString();
        try {
            connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .Build();

            cts = new CancellationTokenSource();

            await connection.StartAsync(cts.Token);

            connected = true;
        }
        catch (Exception exception) {
            status = $"Exception: {exception.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connected) {
            await connection.StopAsync();
            await connection.DisposeAsync();
            connected = false;
        }
    }
}
```

In this component add a small state protocol with two values: START and CANCEL.

> To cancel, if necessary, we use a `CancellationTokenSource` object, with which we can generate an interruption by invoking the `CancelAfter (300) ` method (300 ms is arbitration), or jus t`Cancel ()`.

## Data transmission from Client to Server

Case in which you want the client to send the server a list of objects of the same type, so that these objects are sent one by one or in batches. In practical example it could be that the user loads a file of considerable volume. In this way, the impact on the transmission is light and effective.

The SignalR hub consists of a class that derives from `Hub`,  and one of its asynchronous methods contains a `IAsyncEnumerable parameter<T>`. Let's see the example:

```csharp
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
    }
}
```

> For illustration, I added a message log to see what happens on the server.

**The Client**

The Blazor client, with a reference to `Microsoft.AspNetCore.SignalR.Client`. A component that sends the stream needs a code like the following,

*Blazor StreamOut Component*

```csharp
@using BlazorSignalRStreaming.Shared
@using Microsoft.AspNetCore.SignalR.Client
@page "/streaming-out"
@inject NavigationManager NavigationManager
@implements IAsyncDisposable

<h3><i class="oi oi-cloud-upload"></i> SignalR Streaming Out</h3>
<hr />
<h5>
    The Clint send a stream to Server
</h5>
<br />
<div>
    <button class="btn btn-primary"
            style="width:130px;"
            disabled="@(state=="CANCEL")"
            @onclick="Start">
        Start
    </button>
    <button class="btn btn-danger"
            style="width:130px;"
            disabled="@(state=="START")"
            @onclick="Cancel">
        Cancel
    </button>
</div>
<hr />
<ul style="font-family:Consolas">
    @foreach (var i in ls) {
        <li>@i</li>
    }
</ul>
<p>
    @status
</p>

@code{
    HubConnection connection;
    bool connected;
    bool cancel;
    string state = "START";
    string status;
    List<string> ls = new();
    int sendCount = 20;
    int delay = 300;

    async Task Start()
    {
        state = "CANCEL";
        status = "";
        cancel = false;
        ls.Clear();

        await ConnectAsync();
        if (connected) {
            await connection.SendAsync("UploadStream", ClientStreamData());
        }
    }

    async IAsyncEnumerable<WeatherForecast> ClientStreamData()
    {
        cancel = false;
        for (var i = 1; i <= sendCount; i++) {
            if (cancel) {
                break;
            }
            var item = WeatherForecast.Create(i);
            // send to server
            yield return item;

            ls.Add($"Sending -> {item}");
            StateHasChanged();

            // by illustration
            await Task.Delay(delay);
        }
        status = cancel ? "Canceled" : "Completed";
        state = "START";
        StateHasChanged();
    }

    void Cancel() => cancel = true;

    async Task ConnectAsync()
    {
        if (connected) {
            return;
        }
        var hubUrl = NavigationManager.ToAbsoluteUri("/StreamReceiver").ToString();
        try {
            connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .Build();

            await connection.StartAsync();

            connected = true;
        }
        catch (Exception exception) {
            status = $"Exception: {exception.Message}";
            connected = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (connected) {
            await connection.StopAsync();
            await connection.DisposeAsync();
            connected = false;
        }
    }
}
```

In this component add a small state protocol with two values: START and CANCEL.

> In this case, the client has control of its code and only a boolean will suffice, which by changing its value will break the data sending click.

> The forced wait with the `delay` variable is simply for illustration of behavior.

## Conclusions

In the Blazor world everything that concerns SignalR, by virtue of being the same paradigm, that is C #, is native, clean and solid. Applications that were then complex on the client side with JavaScript as far as the subject matter here is concerned are made easy to improve and debug. With Blazor we have the possibility to go further.

---

This article is part of the BlazorSpread Blog »» [Go to Blog](https://www.blazorspread.net/blogview/using-signalr-streaming-in-blazor).

Official Documentation: [Use streaming in ASP.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/streaming).

---

`MIT license. Author: Harvey Triana. Contact: admin @ blazorspread.net`
