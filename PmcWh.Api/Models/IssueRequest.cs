namespace PmcWh.Api.Models;

public class IssueRequest
{
    public int RecipientId { get; set; }
    public decimal Qty { get; set; }
    public int UserId { get; set; }
}
