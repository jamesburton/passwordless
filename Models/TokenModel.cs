using Newtonsoft.Json;

namespace passwordless.Models
{
    public class TokenModel
    {
        [JsonProperty("token")]
        public string Token { get; set; }
    }
}