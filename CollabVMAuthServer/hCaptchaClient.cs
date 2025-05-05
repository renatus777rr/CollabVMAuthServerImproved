using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Computernewb.CollabVMAuthServer;

public class hCaptchaClient
{
    private string secret;
    private string sitekey;
    private HttpClient http;
    public hCaptchaClient(string secret, string sitekey)
    {
        this.secret = secret;
        this.sitekey = sitekey;
        this.http = new HttpClient();
    }

    public async Task<hCaptchaResponse> Verify(string token, string ip)
    {
        var response = await http.PostAsync("https://api.hcaptcha.com/siteverify", new FormUrlEncodedContent(new []
        {
            new KeyValuePair<string, string>("secret", secret),
            new KeyValuePair<string, string>("response", token),
            new KeyValuePair<string, string>("remoteip", ip),
            new KeyValuePair<string, string>("sitekey", sitekey)
        }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<hCaptchaResponse>() ?? throw new Exception("Failed to parse hCaptcha response");
    }
}

public class hCaptchaResponse
{
    public bool success { get; set; }
    public string challenge_ts { get; set; }
    public string hostname { get; set; }
    public bool? credit { get; set; }
    [JsonPropertyName("error-codes")]
    public string[]? error_codes { get; set; }
}