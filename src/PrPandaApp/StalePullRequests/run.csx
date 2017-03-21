#load "..\CommonFiles\VstsClient.csx"
#load "..\CommonFiles\TeamsClient.csx"
#load "..\CommonFiles\PullRequestSummary.csx"

using System;
using System.Text;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

public async static Task Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

    var personalAccessToken = GetEnvironmentVariable("VstsPersonalAccessToken");
    var collectionUri = new Uri(GetEnvironmentVariable("VstsCollectionUri"));
    var reviewerId = new Guid(GetEnvironmentVariable("VstsReviewerId"));
    var project1 = GetEnvironmentVariable("VstsProject1");
    var project2 = GetEnvironmentVariable("VstsProject2");
    var webHookUri = GetEnvironmentVariable("WebHookUri");

    var historyStorageAccount = CloudStorageAccount.Parse(GetEnvironmentVariable("HistoryStorage"));
    var historyTableClient = historyStorageAccount.CreateCloudTableClient();
    var historyTable = historyTableClient.GetTableReference(GetEnvironmentVariable("HistoryTableName"));
    historyTable.CreateIfNotExists();

    var vstsClient = new VstsClient(collectionUri, personalAccessToken);
    var teamsClient = new TeamsClient(webHookUri);

    var pullRequests = await vstsClient.GetActivePullRequestsAsync(project1, reviewerId);
    log.Info($"Found {pullRequests.Count} active pull requests to check in the {project1}.");

    if (!string.IsNullOrEmpty(project2))
    {
        var pullRequestsFromProjectTwo = await vstsClient.GetActivePullRequestsAsync(project2, reviewerId);
        log.Info($"Found {pullRequestsFromProjectTwo.Count} active pull requests to check in the {project2}.");

        pullRequests.AddRange(pullRequestsFromProjectTwo);
    }

    var stalePullRequests = new List<GitPullRequest>();

    foreach(var pr in pullRequests)
    {
        var threads = await vstsClient.GetCommentThreadsAsync(pr.Repository.Id, pr.PullRequestId);

        if (PullRequestIsStale(historyTable, pr, threads))
        {
            stalePullRequests.Add(pr);
        }
    }

    var messageToPost = GenerateMessage(stalePullRequests);

    await teamsClient.PostMessageAsync(messageToPost);
}

public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}

private static int GetPullRequestHashCode(GitPullRequest pullRequest, List<GitPullRequestCommentThread> commentThreads)
{
    var runningHash = 0;

    unchecked
    {
        foreach (var commentThread in commentThreads)
        {
            runningHash += 10009 * commentThread.Id * commentThread.LastUpdatedDate.GetHashCode();
        }

        runningHash += 10651 * pullRequest.LastMergeSourceCommit.CommitId.GetHashCode();

        foreach (var reviewer in pullRequest.Reviewers)
        {
            runningHash += 11633 * (reviewer.Vote.GetHashCode() + 1) * reviewer.ReviewerUrl.GetHashCode();
        }
    }

    return runningHash;
}

public static bool PullRequestIsStale(CloudTable historyTable, GitPullRequest pullRequest, List<GitPullRequestCommentThread> commentThreads)
{
    var prIdMultiplier = 29;

    var prId = pullRequest.PullRequestId;
    var hashCode = GetPullRequestHashCode(pullRequest, commentThreads);

    var retrieveOperation = TableOperation.Retrieve<PullRequestSummary>((pullRequest.PullRequestId * prIdMultiplier).ToString(), (pullRequest.PullRequestId * prIdMultiplier).ToString());
    var retrievedResult = historyTable.Execute(retrieveOperation);

    if (retrievedResult.Result != null)
    {
        // The PR existed last time this was run.
        var previousSummary = (PullRequestSummary)retrievedResult.Result;
        if (hashCode == previousSummary.HashCode)
        {
            // The PR hasn't changed, it's stale.
            return true;
        }

        // The PR has changed, update its record in the history table.
        previousSummary.HashCode = hashCode;
        var insertOrReplaceOperation = TableOperation.InsertOrReplace(previousSummary);
        historyTable.Execute(insertOrReplaceOperation);

        return false;
    }

    // The PR didn't exist the last time this was run, create its record in the history table.
    var summary = new PullRequestSummary(pullRequest.PullRequestId * prIdMultiplier);
    summary.Id = pullRequest.PullRequestId * prIdMultiplier;
    summary.HashCode = hashCode;

    var insertOperation = TableOperation.Insert(summary);
    historyTable.Execute(insertOperation);

    return false;
}

public static TeamsMessagePayload GenerateMessage(List<GitPullRequest> stalePullRequests)
{
    var message = new TeamsMessagePayload();
    var pullRequestCount = stalePullRequests.Count;
    if (pullRequestCount == 0)
    {
        message.Text = "Great work team, looks like all the pull requests I know about are progressing nicely!";
        message.ThemeColor = "10B51B";

        return message;
    }

    if (pullRequestCount == 1)
    {
        message.Text = $"I found a pull request that hasn't gotten much attention lately and could use some love:";
    }
    else
    { 
        message.Text = $"I found {pullRequestCount} pull requests that haven't gotten much attention lately and could use some love:";
    }

    message.ThemeColor = "FFCC00";
    message.Sections = new List<TeamsSectionPayload>();

    foreach(var pullRequest in stalePullRequests)
    {
        var vstsCollectionUri = pullRequest.Url.Split('_')[0];
        var project = pullRequest.Repository.ProjectReference.Name;
        var pullRequestUri = vstsCollectionUri + "_git/" + project + "/pullrequest/" + pullRequest.PullRequestId;

        var sectionTitle = $"[PR {pullRequest.PullRequestId}]({pullRequestUri}): *{pullRequest.Title}* authored by **{pullRequest.CreatedBy.DisplayName}**.";

        message.Sections.Add(new TeamsSectionPayload { Title = sectionTitle });
    }

    return message;
}