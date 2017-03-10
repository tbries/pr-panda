using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

public class VstsClient
{
    private GitHttpClient client;

    public VstsClient(Uri collectionUri, string personalAccessToken)
    {
        var connection = new VssConnection(collectionUri, new VssBasicCredential(string.Empty, personalAccessToken));
        this.client = connection.GetClient<GitHttpClient>();
    }
}