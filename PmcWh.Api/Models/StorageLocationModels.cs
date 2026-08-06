namespace PmcWh.Api.Models;

public class StorageLocationDto
{
    public int LocationId { get; set; }
    public int RackNo { get; set; }
    public int LevelNo { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class InboundRequest
{
    public int LocationId { get; set; }
    public int UserId { get; set; }
}
