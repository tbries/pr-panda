#load "..\CommonFiles\VstsClient.csx"

using System;

public async static Task Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

    var personalAccessToken = GetEnvironmentVariable("VstsPersonalAccessToken");
    var collectionUri = new Uri(GetEnvironmentVariable("VstsCollectionUri"));
    var reviewerId = new Guid(GetEnvironmentVariable("VstsReviewerId"));
    var project = GetEnvironmentVariable("VstsProject");

    var vstsClient = new VstsClient(collectionUri, personalAccessToken);

    var pullRequests = await vstsClient.GetActivePullRequestsAsync(project, reviewerId);

    foreach(var pr in pullRequests)
    {
        var threads = await vstsClient.GetCommentThreadsAsync(pr.Repository.Id, pr.PullRequestId);

        foreach (var thread in threads)
        {
            var author = await vstsClient.GetCommentThreadAuthorAsync(pr.Repository.Id, pr.PullRequestId, thread.Id);
        }
    }
}

public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}