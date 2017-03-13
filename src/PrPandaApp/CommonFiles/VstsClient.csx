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

    public Task<List<GitPullRequest>> GetActivePullRequestsAsync(string project, Guid ReviewerId)
    {
        var searchCriteria = new GitPullRequestSearchCriteria
        {
            Status = PullRequestStatus.Active,
            ReviewerId = ReviewerId
        };

        return this.client.GetPullRequestsByProjectAsync(project, searchCriteria);
    }

    public async Task<List<GitPullRequestCommentThread>> GetCommentThreadsAsync(Guid repositoryId, int pullRequestId)
    {
        var validCommentThreads = new List<GitPullRequestCommentThread>();
        var allCommentThreads = await this.client.GetThreadsAsync(repositoryId, pullRequestId);

        foreach (var commentThread in allCommentThreads)
        {
            if (commentThread.Status != CommentThreadStatus.Unknown && !commentThread.IsDeleted)
            {
                validCommentThreads.Add(commentThread);
            }
        }

        return validCommentThreads;
    }

    public async Task<IdentityRef> GetCommentThreadAuthorAsync(Guid repositoryId, int pullRequestId, int threadId)
    {
        var parentComment = await client.GetCommentAsync(repositoryId, pullRequestId, threadId, 1);

        return parentComment.Author;
    }
}