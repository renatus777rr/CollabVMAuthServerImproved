using System.Text.Json.Serialization;

namespace Computernewb.CollabVMAuthServer.HTTP.Payloads;

public class RequestBodyAuthenticationPayload {
    [JsonPropertyName("session")]
    public string? Session { get; set; }
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}