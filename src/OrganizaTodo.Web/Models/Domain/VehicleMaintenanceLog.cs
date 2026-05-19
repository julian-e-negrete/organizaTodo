namespace OrganizaTodo.Web.Models.Domain;

public sealed class VehicleMaintenanceLog
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public int UserId { get; set; }
    public DateOnly ServiceDate { get; set; }
    public decimal KmAtService { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
