using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;

public class TeamsClient
{
    private readonly Uri webhookUri;
    private readonly Encoding encoding = new UTF8Encoding();

    public TeamsClient(string urlWithAccessToken)
    {
        webhookUri = new Uri(urlWithAccessToken);
    }

    public Task PostMessageAsync(string text, string themeColor = "006AFF")
    {
        var payload = new Payload()
        {
            Text = text,
            ThemeColor = themeColor
        };

        return PostMessageAsync(payload);
    }

    public async Task PostMessageAsync(Payload payload)
    {
        //var payloadJson = JsonConvert.SerializeObject(payload);

        //using (var client = new WebClient())
        //{
        //    var data = new NameValueCollection
        //    {
        //        ["payload"] = payloadJson
        //    };

        //    var response = client.UploadValues(webhookUri, "POST", data);
        //}

        using (var client = new HttpClient())
        {
            var response = await client.PostAsJsonAsync(webhookUri, payload);
        }
    }
}

public class Payload
{
    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("themeColor")]
    public string ThemeColor { get; set; }
}