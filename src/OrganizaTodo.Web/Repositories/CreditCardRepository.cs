using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class CreditCardRepository(IDbConnectionFactory connectionFactory) : ICreditCardRepository
{
    public async Task<IEnumerable<CreditCardPurchase>> GetByUserIdAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<CreditCardPurchase>(
            "SELECT * FROM sp_credit_card_get_by_user_id(@userId, @month, @year)",
            new { userId, month, year });
    }

    public async Task<CreditCardPurchase?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<CreditCardPurchase>(
            "SELECT * FROM sp_credit_card_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task CreateAsync(int userId, string description, decimal totalAmount, int installments,
        decimal interestRate, DateTime purchaseDate, int dueMonth, int dueYear)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_create(@userId, @description, @totalAmount, @installments, @interestRate, @purchaseDate, @dueMonth, @dueYear)",
            new { userId, description, totalAmount, installments, interestRate, purchaseDate, dueMonth, dueYear });
    }

    public async Task UpdateAsync(int id, int userId, string description, decimal totalAmount,
        int installments, decimal interestRate, int dueMonth, int dueYear)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_credit_card_update(@id, @userId, @description, @totalAmount, @installments, @interestRate, @dueMonth, @dueYear)",
            new { id, userId, description, totalAmount, installments, interestRate, dueMonth, dueYear });
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

    public async Task<decimal> GetMonthlyTotalAsync(int userId, int month, int year)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_credit_card_get_monthly_total(@userId, @month, @year)",
            new { userId, month, year });
    }
}
