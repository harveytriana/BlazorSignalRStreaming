# Using the MessagePack Protocol in SignalR and Blazor Client

«MessagePack is a fast and compact binary serialization format. Ideal when performance and bandwidth are an issue because it creates smaller messages compared to JSON.» I will focus on showing how to enable the protocol in a Blazor application. As an example I take the classic `WeatherForecast` service, enable it to generate a simple SignalR service, and finally enable `MessagePack`. I use a Blazor application hosted on ASP.NET Core.

**The model**

`WeatherForecast` is extended to include a more elaborate object, `WeatherReport`.

```csharp
namespace BlazorMessagePack.Shared
{
    public class WeatherForecast
    {
        public DateTime Date { get; set; }
        public int TemperatureC { get; set; }
        public string Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }

    public class WeatherReport
    {
        public DateTime ReportDate { get; set; }
        public IEnumerable<WeatherForecast> Forecasts { get; set; }
    }
}
```

**The Hub**

A simple method, `GenerateReport`,  is added that delivers to SignalR clients a `WeatherReport` object with a discrete number of `WeatherForecast` records.

```csharp
using BlazorMessagePack.Shared;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorMessagePack.Server.Hubs
{
    public class WeatherForecastHub : Hub
    {
        static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        static readonly Random _random = new();

        public async Task GenerateReport(int rows)
        {
            var forecasts = Enumerable.Range(1, rows).Select(index => new WeatherForecast {
                Date = DateTime.UtcNow.AddMinutes(index),
                TemperatureC = _random.Next(-20, 55),
                Summary = Summaries[_random.Next(Summaries.Length)]
            });

            var report = new WeatherReport {
                ReportDate = DateTime.UtcNow,
                Forecasts = forecasts
            };

            await Clients.All.SendAsync("Report", report);
        }
    }
}
```

**The configuration**

Add the `AddMessagePackProtocol()` directive to the SignalR service disposition. It is worth mentioning that we can still use JSON if the client wishes, since it remains with the two protocols available. Simplify the Startup class to highlight what is relevant to the topic discussed here.

```csharp
// ...
namespace BlazorMessagePack.Server
{
    public class Startup
    {
        // ...
        public void ConfigureServices(IServiceCollection services)
        {
            // ...

            #region SignalR
            services.AddSignalR().AddMessagePackProtocol();

            services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/octet-stream" });
            });
            #endregion
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            p.UseEndpoints(endpoints => {
                // ...
                endpoints.MapHub<WeatherForecastHub>("/WeatherForecast");
            });
        }
    }
}
```

**The client**

Basically we enhance the connection constructs the `AddMessagePackProtocol()` directive

*Requires that we install Microsoft.AspNetCore.SignalR.Protocols.MessagePack*

```csharp
@page "/fetchdata"
@using BlazorMessagePack.Shared
@using Microsoft.AspNetCore.SignalR.Client
@using Microsoft.Extensions.DependencyInjection
@inject NavigationManager NavigationManager
@implements IAsyncDisposable

<h1>Weather forecast</h1>
<hr />
<p>This component demonstrates fetching data from the server.</p>
<p>
    <button class="btn btn-primary"
            disabled="@disconnected"
            @onclick="GenerateReport">
        Generate Report
    </button>
</p>
<br />
@if (forecasts != null) {
    <h4>Report: @reportDate.ToShortDateString()  @reportDate.ToLongTimeString()</h4>
    <table class="table table-sm" style="font-family:Courier New, Courier, monospace">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var forecast in forecasts) {
                <tr>
                    <td>@forecast.Date.ToLocalTime().ToShortTimeString()</td>
                    <td>@forecast.TemperatureC</td>
                    <td>@forecast.TemperatureF</td>
                    <td>@forecast.Summary</td>
                </tr>
            }
        </tbody>
    </table>
    <hr />
    <p>Records: @forecasts.Length</p>
}

@code {
    WeatherForecast[] forecasts;
    DateTime reportDate;
    HubConnection hubConnection;
    bool disconnected = true;

    protected override async Task OnInitializedAsync()
    {
        try {
            hubConnection = new HubConnectionBuilder()
               .WithUrl(NavigationManager.ToAbsoluteUri("/WeatherForecast"))
               .AddMessagePackProtocol()
               .Build();
            await hubConnection.StartAsync();

            disconnected = hubConnection.State == HubConnectionState.Disconnected;
        }
        catch (Exception e) {
            Console.WriteLine("Exception: {0}", e.Message);
        }

        hubConnection.On<WeatherReport>("Report", (weatherReport) => {
            reportDate = weatherReport.ReportDate.ToLocalTime();
            forecasts = weatherReport.Forecasts.ToArray();
            StateHasChanged();
        });
    }

    async Task GenerateReport()
    {
        await hubConnection.SendAsync("GenerateReport", 50);
    }

    public async ValueTask DisposeAsync()
    {
        await hubConnection.DisposeAsync();
    }
}
```

> As an additional and important detail, keep in mind the way in which I manage the server time and the transformation to local time on the client.

NOTE. *The efficiency of MesagePack is remarkable since it is a transmission in binary format. However, it becomes noticeable when the message is "big", or in a continuous sending of data in real time. See https://msgpack.org/ for more information.*

---

By: [Blazor Spread](https://www.blazorspread.net) - Harvey Triana
