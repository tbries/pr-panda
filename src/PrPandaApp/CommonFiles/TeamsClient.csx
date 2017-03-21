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
        var payload = new TeamsMessagePayload()
        {
            Text = text,
            ThemeColor = themeColor
        };

        return PostMessageAsync(payload);
    }

    public async Task PostMessageAsync(TeamsMessagePayload payload)
    {
        using (var client = new HttpClient())
        {
            var response = await client.PostAsJsonAsync(webhookUri, payload);
        }
    }
}

public class TeamsMessagePayload
{
    [JsonProperty("text")]
    public string Text { get; set; }

    [JsonProperty("themeColor")]
    public string ThemeColor { get; set; }

    [JsonProperty("sections")]
    public List<TeamsSectionPayload> Sections { get; set; }
}

public class TeamsSectionPayload
{
    [JsonProperty("title")]
    public string Title { get; set; }
}