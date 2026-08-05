namespace PmcWh.Api.Models;

public class RecipientDto
{
    public int RecipientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateRecipientRequest
{
    public string Name { get; set; } = string.Empty;
}
