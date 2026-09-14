namespace Warehouse3D.Models;

public class OracleConnectionOptions
{
    public const string SectionName = "Oracle";

    public string? ConnectionString { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? DataSource { get; set; }
}
