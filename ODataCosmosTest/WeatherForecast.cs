using System.ComponentModel.DataAnnotations;

namespace ODataCosmosTest
{
    public class WeatherForecast
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        public string? Summary { get; set; }
    }
}