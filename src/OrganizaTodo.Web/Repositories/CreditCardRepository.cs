using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class CreditCardRepository(IDbConnectionFactory connectionFactory) : ICreditCardRepository
{
    public async Task<IEnumerable<CreditCardPurchase>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<CreditCardPurchase>(
            "SELECT * FROM sp_credit_card_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<CreditCardPurchase?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<CreditCardPurchase>(
            "SELECT * FROM sp_credit_card_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string description, decimal totalAmount, int installments, decimal interestRate, DateTime purchaseDate)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_create(@userId, @description, @totalAmount, @installments, @interestRate, @purchaseDate)",
            new { userId, description, totalAmount, installments, interestRate, purchaseDate });
    }

    public async Task UpdateAsync(int id, int userId, string description, decimal totalAmount, int installments, decimal interestRate)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_update(@id, @userId, @description, @totalAmount, @installments, @interestRate)",
            new { id, userId, description, totalAmount, installments, interestRate });
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_delete(@id, @userId)",
            new { id, userId });
    }

    public async Task AdvanceInstallmentAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_advance_installment(@id, @userId)",
            new { id, userId });
    }

    public async Task<decimal> GetMonthlyTotalAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_credit_card_get_monthly_total(@userId)",
            new { userId });
    }
}
