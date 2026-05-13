using Newtonsoft.Json;

namespace Models
{
    public class WebhookEvent
    {
        [JsonProperty("clientID")]
        public string ClientID { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("iv")]
        public string IV { get; set; }

        [JsonProperty("crypto_version")]
        public string CryptoVersion { get; set; }

        [JsonProperty("ciphertext")]
        public string Ciphertext { get; set; }

        [JsonProperty("encrypted_key")]
        public string EncryptedKey { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("tag")]
        public string Tag { get; set; }
    }
}
