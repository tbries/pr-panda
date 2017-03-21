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

    if(!string.IsNullOrEmpty(project2))
    {
        var pullRequestsFromProjectTwo = await vstsClient.GetActivePullRequestsAsync(project2, reviewerId);
        log.Info($"Found {pullRequestsFromProjectTwo.Count} active pull requests to check in the {project2}.");

        pullRequests.AddRange(pullRequestsFromProjectTwo);
    }

    foreach (var pr in pullRequests)
    {
        var threads = await vstsClient.GetCommentThreadsAsync(pr.Repository.Id, pr.PullRequestId);

        if (PullRequestNeedsToBePosted(historyTable, pr, threads))
        {
            var commentAuthors = await GetPullRequestCommenterNamesAsStringAsync(vstsClient, pr, threads);
            var messageText = GenerateMessageText(pr, commentAuthors);

            await teamsClient.PostMessageAsync(messageText);
        }
    }
}

public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}

public static bool PullRequestNeedsToBePosted(CloudTable historyTable, GitPullRequest pullRequest, List<GitPullRequestCommentThread> commentThreads)
{
    if (!IsPullRequestReadyToBeReReviewed(commentThreads))
    {
        // Pull request is not ready to be reviewed, no action is needed.
        return false;
    }

    var threadsHashCode = GetCommentThreadsHashCode(commentThreads);

    var retrieveOperation = TableOperation.Retrieve<PullRequestSummary>(pullRequest.PullRequestId.ToString(), pullRequest.PullRequestId.ToString());
    var retrievedResult = historyTable.Execute(retrieveOperation);

    if (retrievedResult.Result != null)
    {
        // The PR has been posted before.
        var previousSummary = (PullRequestSummary)retrievedResult.Result;
        if (threadsHashCode == previousSummary.HashCode)
        {
            // The PR hasn't changed, don't post it.
            return false;
        }

        // The PR has changed, update its record in the history table.
        previousSummary.HashCode = threadsHashCode;
        var insertOrReplaceOperation = TableOperation.InsertOrReplace(previousSummary);
        historyTable.Execute(insertOrReplaceOperation);

        return true;
    }

    // The PR has not been posted before, create its record in the history table.
    var summary = new PullRequestSummary(pullRequest.PullRequestId);
    summary.Id = pullRequest.PullRequestId;
    summary.HashCode = threadsHashCode;

    var insertOperation = TableOperation.Insert(summary);
    historyTable.Execute(insertOperation);

    return true;
}

public static bool IsPullRequestReadyToBeReReviewed(List<GitPullRequestCommentThread> commentThreads)
{
    var activeThreadsCount = 0;
    var resolvedThreadsCount = 0;

    foreach (var thread in commentThreads)
    {
        switch (thread.Status)
        {
            case CommentThreadStatus.Active:
            case CommentThreadStatus.Pending:
            activeThreadsCount++;
            break;

            case CommentThreadStatus.Fixed: // (Resolved)
            case CommentThreadStatus.Closed:
            case CommentThreadStatus.WontFix:
            case CommentThreadStatus.ByDesign:
            resolvedThreadsCount++;
            break;

            case CommentThreadStatus.Unknown: // System threads.
            break;
        }
    }

    return activeThreadsCount == 0 && resolvedThreadsCount != 0;
}

public static int GetCommentThreadsHashCode(List<GitPullRequestCommentThread> commentThreads)
{
    int hashCode = 0;

    foreach (var thread in commentThreads)
    {
        switch (thread.Status)
        {
            case CommentThreadStatus.Active:
            case CommentThreadStatus.Pending:
                unchecked
                {
                    hashCode += 1619 * thread.Id;
                }

                break;

            case CommentThreadStatus.Fixed: // (Resolved)
            case CommentThreadStatus.Closed:
            case CommentThreadStatus.WontFix:
            case CommentThreadStatus.ByDesign:
                unchecked
                {
                    hashCode += 1223 * thread.Id;
                }

                break;

            case CommentThreadStatus.Unknown: // System threads.
                break;
        }
    }

    return hashCode;
}

public static async Task<string> GetPullRequestCommenterNamesAsStringAsync(VstsClient vstsClient, GitPullRequest pullRequest, List<GitPullRequestCommentThread> commentThreads)
{
    var commenterDisplayNames = new List<string>();

    foreach (var thread in commentThreads)
    {
        var commentAuthor = await vstsClient.GetCommentThreadAuthorAsync(pullRequest.Repository.Id, pullRequest.PullRequestId, thread.Id);
        var authorName = commentAuthor.DisplayName;

        if(!commenterDisplayNames.Contains(authorName))
        {
            commenterDisplayNames.Add(authorName);
        }
    }

    var builder = new StringBuilder(commenterDisplayNames.First());
    for (var i = 1; i < commenterDisplayNames.Count - 1; i++)
    {
        builder.Append(", " + commenterDisplayNames[i]);
    }

    if (commenterDisplayNames.Count > 1)
    {
        builder.Append(" & " + commenterDisplayNames.Last());
    }

    return builder.ToString();
}

public static string GenerateMessageText(GitPullRequest pullRequest, string commenterList)
{
    var vstsCollectionUri = pullRequest.Url.Split('_')[0];
    var project = pullRequest.Repository.ProjectReference.Name;
    var pullRequestUri = vstsCollectionUri + "_git/" + project + "/pullrequest/" + pullRequest.PullRequestId;

    return $"**{pullRequest.CreatedBy.DisplayName}** has addressed all of the comments from **{commenterList}** on [pull request {pullRequest.PullRequestId}]({pullRequestUri}) *{pullRequest.Title}*";
}