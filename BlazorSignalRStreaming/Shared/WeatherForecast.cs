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
