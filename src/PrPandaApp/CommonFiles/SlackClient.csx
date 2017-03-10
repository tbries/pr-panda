using System;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using Newtonsoft.Json;

public class SlackClient
{
    private readonly Uri webhookUri;
    private readonly Encoding encoding = new UTF8Encoding();

    public SlackClient(string urlWithAccessToken)
    {
        webhookUri = new Uri(urlWithAccessToken);
    }

    public void PostMessage(string text, string username = null, string channel = null)
    {
        var payload = new Payload()
        {
            Channel = channel,
            Username = username,
            Text = text
        };

        PostMessage(payload);
    }

    public void PostMessage(Payload payload)
    {
        var payloadJson = JsonConvert.SerializeObject(payload);

        using (var client = new WebClient())
        {
            var data = new NameValueCollection
            {
                ["payload"] = payloadJson
            };

            var response = client.UploadValues(webhookUri, "POST", data);
        }
    }
}

public class Payload
{
    [JsonProperty("channel")]
    public string Channel { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }
}