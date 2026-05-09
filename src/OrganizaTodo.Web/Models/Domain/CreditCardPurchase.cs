namespace OrganizaTodo.Web.Models.Domain;

public sealed class CreditCardPurchase
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int Installments { get; set; } = 1;
    public int CurrentInstallment { get; set; } = 1;
    public decimal InterestRate { get; set; }
    public DateTime PurchaseDate { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal MonthlyInstallmentAmount { get; set; }

    public int RemainingInstallments => Math.Max(0, Installments - CurrentInstallment + 1);
}
