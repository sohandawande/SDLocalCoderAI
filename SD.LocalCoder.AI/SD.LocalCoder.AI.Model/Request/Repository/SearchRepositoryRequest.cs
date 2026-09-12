namespace SD.LocalCoder.AI.Model.Request.Repository;

public sealed class SearchRepositoryRequest
{
    public string Query { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 20;
}
