using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public interface ICreditCardRepository
{
    Task<IEnumerable<CreditCardPurchase>> GetByUserIdAsync(int userId);
    Task<CreditCardPurchase?> GetByIdAsync(int id, int userId);
    Task CreateAsync(int userId, string description, decimal totalAmount, int installments, decimal interestRate, DateTime purchaseDate);
    Task UpdateAsync(int id, int userId, string description, decimal totalAmount, int installments, decimal interestRate);
    Task DeleteAsync(int id, int userId);
    Task AdvanceInstallmentAsync(int id, int userId);
    Task<decimal> GetMonthlyTotalAsync(int userId);
}
