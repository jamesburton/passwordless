using Newtonsoft.Json;

namespace passwordless.Models
{
    public class User {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("email")]
	    public string Email { get; set; }
        [JsonProperty("token")]
        public string Token { get; set; }
        [JsonProperty("shortCode")]
        public string ShortCode { get; set; }
    }
}
