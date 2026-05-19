using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface ICreditCardRepository
{
    Task<IEnumerable<CreditCardPurchase>> GetByUserIdAsync(int userId, int month, int year);
    Task<CreditCardPurchase?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string description, decimal totalAmount, int installments,
        decimal interestRate, DateTime purchaseDate, int dueMonth, int dueYear);
    Task UpdateAsync(int id, int userId, string description, decimal totalAmount,
        int installments, decimal interestRate, int dueMonth, int dueYear);
    Task DeleteAsync(int id, int userId);
    Task AdvanceInstallmentAsync(int id, int userId);
    Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year);
}
