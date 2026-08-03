namespace PmcWh.Web.Models;

public class DashboardViewModel
{
    public string Title { get; set; } = "Dashboard";
    public List<DashboardCard> Cards { get; set; } = new();
}

public class DashboardCard
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "primary";
}
