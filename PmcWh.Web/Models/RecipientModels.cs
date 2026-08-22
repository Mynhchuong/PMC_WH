using System.ComponentModel.DataAnnotations;

namespace PmcWh.Web.Models;

public class RecipientDto
{
    public int RecipientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class RecipientListViewModel
{
    public List<RecipientDto> Items { get; set; } = new();
    public CreateRecipientViewModel NewRecipient { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = new();
}

public class CreateRecipientViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên nơi nhận")]
    [Display(Name = "Nơi nhận")]
    public string Name { get; set; } = string.Empty;
}
