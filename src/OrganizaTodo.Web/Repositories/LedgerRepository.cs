using System.Data;
using Dapper;
using OrganizaTodo.Web.Data;
using OrganizaTodo.Web.Models.Domain;

namespace OrganizaTodo.Web.Repositories;

public sealed class LedgerRepository(IDbConnectionFactory connectionFactory) : ILedgerRepository
{
    private sealed class LedgerHistoryRow : LedgerTransaction
    {
        public long TotalCount { get; set; }
    }

    private sealed class AnchorExpenseRow
    {
        public int Id { get; set; }
        public decimal InterestRate { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    private sealed class InterestPointersRow
    {
        public DateTime? LastInterestDate { get; set; }
        public DateTime? FirstAnchorExpenseDate { get; set; }
    }

    public async Task<LedgerTag> GetOrCreateTagAsync(int userId, string name)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleAsync<LedgerTag>(
            "SELECT * FROM sp_ledger_tags_get_or_create(@userId, @name)",
            new { userId, name });
    }

    public async Task<IEnumerable<LedgerTag>> GetTagsByUserIdAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<LedgerTag>(
            "SELECT * FROM sp_ledger_tags_get_by_user_id(@userId)",
            new { userId });
    }

    public async Task<decimal> GetCurrentBalanceAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_ledger_balance_get_current(@userId)",
            new { userId });
    }

    public async Task<decimal> GetBalanceAsOfAsync(int userId, DateTime asOf)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<decimal>(
            "SELECT sp_ledger_balance_get_as_of(@userId, @asOf)",
            new { userId, asOf });
    }

    public async Task<long> GetTransactionCountAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<long>(
            "SELECT sp_ledger_transactions_get_count(@userId)",
            new { userId });
    }

    public async Task<int> CreateExpenseAsync(int userId, decimal amount, string description, DateTime transactionDate, bool generatesInterest, decimal? interestRate)
    {
        var p = new DynamicParameters();
        p.Add("p_user_id", userId, DbType.Int32);
        p.Add("p_amount", amount, DbType.Decimal);
        p.Add("p_description", description, DbType.String);
        p.Add("p_transaction_date", transactionDate, DbType.DateTime2);
        p.Add("p_generates_interest", generatesInterest, DbType.Boolean);
        p.Add("p_interest_rate", interestRate, DbType.Decimal);
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT sp_ledger_transaction_create_expense(@p_user_id, @p_amount, @p_description, @p_transaction_date, @p_generates_interest, @p_interest_rate)", p);
    }

    public async Task<int> CreateIncomeAsync(int userId, decimal amount, string description, DateTime transactionDate)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT sp_ledger_transaction_create_income(@userId, @amount, @description, @transactionDate)",
            new { userId, amount, description, transactionDate });
    }

    public async Task<int> CreateInitialAsync(int userId, decimal amount, DateTime transactionDate)
    {
        using var conn = connectionFactory.CreateConnection();
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT sp_ledger_transaction_create_initial(@userId, @amount, @transactionDate)",
            new { userId, amount, transactionDate });
        return id ?? 0;
    }

    public async Task<int> CreateInterestAsync(int userId, decimal amount, DateTime transactionDate, decimal interestRate, int parentTransactionId, decimal balanceBefore)
    {
        var p = new DynamicParameters();
        p.Add("p_user_id", userId, DbType.Int32);
        p.Add("p_amount", amount, DbType.Decimal);
        p.Add("p_transaction_date", transactionDate, DbType.DateTime2);
        p.Add("p_interest_rate", interestRate, DbType.Decimal);
        p.Add("p_parent_transaction_id", parentTransactionId, DbType.Int32);
        p.Add("p_balance_before", balanceBefore, DbType.Decimal);
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT sp_ledger_transaction_create_interest(@p_user_id, @p_amount, @p_transaction_date, @p_interest_rate, @p_parent_transaction_id, @p_balance_before)", p);
    }

    public async Task LinkTagAsync(int transactionId, int tagId)
    {
        using var conn = connectionFactory.CreateConnection();
        await conn.QueryFirstOrDefaultAsync<object>(
            "SELECT sp_ledger_tags_link(@transactionId, @tagId)",
            new { transactionId, tagId });
    }

    public async Task<(IEnumerable<LedgerTransaction> Items, long TotalCount)> GetHistoryAsync(int userId, LedgerHistoryFilter filter, int page, int pageSize)
    {
        var p = new DynamicParameters();
        p.Add("p_user_id", userId, DbType.Int32);
        p.Add("p_date_from", filter.DateFrom, DbType.DateTime2);
        p.Add("p_date_to", filter.DateTo, DbType.DateTime2);
        p.Add("p_type", filter.Type, DbType.String);
        p.Add("p_tag_ids", filter.TagIds ?? []);
        p.Add("p_amount_min", filter.AmountMin, DbType.Decimal);
        p.Add("p_amount_max", filter.AmountMax, DbType.Decimal);
        p.Add("p_only_with_interest", filter.OnlyWithInterest, DbType.Boolean);
        p.Add("p_only_negative_balance", filter.OnlyNegativeBalance, DbType.Boolean);
        p.Add("p_search", filter.Search, DbType.String);
        p.Add("p_page", page, DbType.Int32);
        p.Add("p_page_size", pageSize, DbType.Int32);

        using var conn = connectionFactory.CreateConnection();
        var rows = (await conn.QueryAsync<LedgerHistoryRow>(
            @"SELECT * FROM sp_ledger_history_get(
                @p_user_id, @p_date_from, @p_date_to, @p_type, @p_tag_ids, @p_amount_min, @p_amount_max,
                @p_only_with_interest, @p_only_negative_balance, @p_search, @p_page, @p_page_size)", p)).ToList();

        var totalCount = rows.Count > 0 ? rows[0].TotalCount : 0;
        return (rows, totalCount);
    }

    public async Task<IEnumerable<LedgerBalancePoint>> GetBalanceSeriesAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<LedgerBalancePoint>(
            "SELECT * FROM sp_ledger_chart_get_balance_series(@userId)",
            new { userId });
    }

    public async Task<LedgerTransaction?> GetDetailAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<LedgerTransaction>(
            "SELECT * FROM sp_ledger_transaction_get_detail(@id, @userId)",
            new { id, userId });
    }

    public async Task<IEnumerable<LedgerTransactionChild>> GetChildrenAsync(int id, int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<LedgerTransactionChild>(
            "SELECT * FROM sp_ledger_transaction_get_children(@id, @userId)",
            new { id, userId });
    }

    public async Task<(int Id, decimal InterestRate, DateTime TransactionDate)?> GetLatestAnchorExpenseAsync(int userId, DateTime monthEnd)
    {
        using var conn = connectionFactory.CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<AnchorExpenseRow>(
            "SELECT * FROM sp_ledger_anchor_expense_get_latest_for_month(@userId, @monthEnd)",
            new { userId, monthEnd });
        return row is null ? null : (row.Id, row.InterestRate, row.TransactionDate);
    }

    public async Task<(DateTime? LastInterestDate, DateTime? FirstAnchorExpenseDate)> GetInterestPointersAsync(int userId)
    {
        using var conn = connectionFactory.CreateConnection();
        var row = await conn.QuerySingleAsync<InterestPointersRow>(
            "SELECT * FROM sp_ledger_interest_get_pointers(@userId)",
            new { userId });
        return (row.LastInterestDate, row.FirstAnchorExpenseDate);
    }
}
