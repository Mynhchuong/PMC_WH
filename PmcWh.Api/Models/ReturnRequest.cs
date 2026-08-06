namespace PmcWh.Api.Models;

public class ReturnRequest
{
    public int LocationId { get; set; }
    public decimal Qty { get; set; }
    public int UserId { get; set; }
}
