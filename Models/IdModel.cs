using Newtonsoft.Json;

namespace passwordless.Models
{
    public class IdModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }
}