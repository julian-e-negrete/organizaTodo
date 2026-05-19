namespace OrganizaTodo.Web.Models.Domain;

public sealed class Vehicle
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Plate { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int? Year { get; set; }
    public decimal WeeklyKm { get; set; }
    public decimal FuelEfficiencyKmL { get; set; }
    public decimal? FuelPricePerLiter { get; set; }
    public decimal? InsuranceMonthly { get; set; }
    public decimal? PatenteAnnual { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal MonthlyFuelCost { get; set; }
    public decimal MonthlyPatente { get; set; }
    public decimal TotalMonthlyCost { get; set; }
    public bool FuelPaid { get; set; }
    public bool InsurancePaid { get; set; }
    public bool PatentePaid { get; set; }
    public decimal? OilChangeIntervalKm { get; set; }
    public decimal? OilChangeCostEstimate { get; set; }
    public DateOnly? LastOilChangeDate { get; set; }
    public decimal? LastOilChangeKm { get; set; }
    public decimal MonthlyOilChangeCost { get; set; }
    public decimal? KmSinceLastChange { get; set; }
    public decimal? KmToNextChange { get; set; }
    public DateOnly? NextOilChangeDueDate { get; set; }
    public bool MaintenancePaid { get; set; }
}
