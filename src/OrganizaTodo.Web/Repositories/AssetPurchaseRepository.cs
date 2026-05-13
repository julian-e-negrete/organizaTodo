using System.Data;
using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class AssetPurchaseRepository(IDbConnectionFactory connectionFactory) : IAssetPurchaseRepository
{
    public async Task<IEnumerable<AssetPurchase>> GetByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<AssetPurchase>(
            "SELECT * FROM sp_asset_purchases_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<AssetPurchase?> GetByIdAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AssetPurchase>(
            "SELECT * FROM sp_asset_purchases_get_by_id(@id, @userId)",
            new { id, userId });
    }

    public async Task<int> CreateAsync(
        int userId, string assetType, decimal? quantity, decimal? unitPriceArs,
        string? exchangeType, decimal totalArs, string? notes, DateOnly purchaseDate,
        bool createExpense, string? expenseDescription, int expenseMonth, int expenseYear,
        bool createSaving, string? savingNotes, int savingMonth, int savingYear)
    {
        var p = new DynamicParameters();
        p.Add("p_user_id",        userId,             DbType.Int32);
        p.Add("p_asset_type",     assetType,          DbType.String);
        p.Add("p_quantity",       quantity,           DbType.Decimal);
        p.Add("p_unit_price_ars", unitPriceArs,       DbType.Decimal);
        p.Add("p_exchange_type",  exchangeType,       DbType.String);
        p.Add("p_total_ars",      totalArs,           DbType.Decimal);
        p.Add("p_notes",          notes,              DbType.String);
        p.Add("p_purchase_date",  purchaseDate,       DbType.Date);
        p.Add("p_create_expense", createExpense,      DbType.Boolean);
        p.Add("p_expense_desc",   expenseDescription, DbType.String);
        p.Add("p_expense_month",  expenseMonth,       DbType.Int32);
        p.Add("p_expense_year",   expenseYear,        DbType.Int32);
        p.Add("p_create_saving",  createSaving,       DbType.Boolean);
        p.Add("p_saving_notes",   savingNotes,        DbType.String);
        p.Add("p_saving_month",   savingMonth,        DbType.Int32);
        p.Add("p_saving_year",    savingYear,         DbType.Int32);
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            @"SELECT sp_asset_purchases_create(
                @p_user_id, @p_asset_type, @p_quantity, @p_unit_price_ars, @p_exchange_type,
                @p_total_ars, @p_notes, @p_purchase_date,
                @p_create_expense, @p_expense_desc, @p_expense_month, @p_expense_year,
                @p_create_saving, @p_saving_notes, @p_saving_month, @p_saving_year)", p);
    }

    public async Task UpdateAsync(
        int id, int userId, string assetType, decimal? quantity, decimal? unitPriceArs,
        string? exchangeType, decimal totalArs, string? notes, DateOnly purchaseDate)
    {
        var p = new DynamicParameters();
        p.Add("id",           id,                DbType.Int32);
        p.Add("userId",       userId,            DbType.Int32);
        p.Add("assetType",    assetType,         DbType.String);
        p.Add("quantity",     quantity,          DbType.Decimal);
        p.Add("unitPriceArs", unitPriceArs,      DbType.Decimal);
        p.Add("exchangeType", exchangeType,      DbType.String);
        p.Add("totalArs",     totalArs,          DbType.Decimal);
        p.Add("notes",        notes,             DbType.String);
        p.Add("purchaseDate", purchaseDate,       DbType.Date);
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_asset_purchases_update(@id, @userId, @assetType, @quantity, @unitPriceArs, @exchangeType, @totalArs, @notes, @purchaseDate)", p);
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_asset_purchases_delete(@id, @userId)",
            new { id, userId });
    }
}
