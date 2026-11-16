using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class BillAmountHistoryRepository(IDbConnectionFactory connectionFactory) : IBillAmountHistoryRepository
{
    public async Task<IReadOnlyList<BillAmountChange>> GetForBillAsync(int userId, string billType, int billId)
    {
        using var conn = connectionFactory.CreateConnection();
        var rows = await conn.QueryAsync<BillAmountChange>(
            "SELECT * FROM sp_bill_amount_history_get(@userId, @billType, @billId)",
            new { userId, billType, billId });
        return rows.AsList();
    }
}
