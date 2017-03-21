using Microsoft.WindowsAzure.Storage.Table;

public class PullRequestSummary : TableEntity
{
    public int Id { get; set; }
    public int HashCode { get; set; }
    
    public PullRequestSummary(int id)
    {
        this.PartitionKey = id.ToString();
        this.RowKey = id.ToString();
    }
    
    public PullRequestSummary()
    {
    }
}