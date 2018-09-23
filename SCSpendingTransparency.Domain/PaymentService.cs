using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using SCSpendingTransparency.Data;
using SCSpendingTransparency.Data.Models;
using Dapper;
using NLog;

namespace SCSpendingTransparency.Domain
{
	public class PaymentService
	{
		private readonly ApplicationDbContext _db;
        private readonly ILogger _logger;

		public PaymentService(ApplicationDbContext db, ILogger logger)
		{
		    _db = db;
		    _logger = logger;
		}

		public async Task<bool> CreatePayment(string agencyId, string agency, string category, string expense, string payee, string docId, DateTime transactionDate, string fund, string subFund, decimal amount, IDbTransaction transaction = null)
		{
		    var payment = new Payment()
			{
				Id = Guid.NewGuid(),
				AgencyId = agencyId,
				Agency = agency,
				Amount = amount,
				Category = category,
				DocId = docId,
				Expense = expense,
				Fund = fund,
				Payee = payee,
				SubFund = subFund,
				TransactionDate = transactionDate,
                InsertedAt = DateTimeOffset.UtcNow,
			};

		    payment.Hash = PaymentHasher.Hash(payment);

		    var existing = await _db.Database.Connection.ExecuteScalarAsync<int>(
		        "select count(*) as existing from Payments where Hash = @Hash", new {Hash = payment.Hash}, transaction);

		    if (existing == 1)
		    {
		        return true;
		    }

            var result = await _db.Database.Connection.ExecuteAsync(@"
insert into payments ( 
    Id, 
    AgencyId, 
    Agency, 
    Amount, 
    Category, 
    DocId, 
    Expense, 
    Fund, 
    Payee, 
    SubFund, 
    TransactionDate, 
    InsertedAt,
    Hash
) values (
    @Id, 
    @AgencyId, 
    @Agency, 
    @Amount, 
    @Category, 
    @DocId, 
    @Expense, 
    @Fund, 
    @Payee, 
    @SubFund, 
    @TransactionDate, 
    @InsertedAt,
    @Hash
)", payment, transaction);

		    if (result == 1)
		    {
		        return true;
		    }
		    else
		    {
		        throw new Exception("Error inserting Payment record!");
		    }
		}

	    public async Task<bool> CreatePaymentBatch(IEnumerable<PaymentForWrite> payments, int batchSize = 1000)
	    {
	        try
	        {
	            var transaction = _db.Database.BeginTransaction(IsolationLevel.ReadCommitted);

	            var i = 0;
	            foreach (var payment in payments)
	            {
	                try
	                {
	                    await CreatePayment(payment.AgencyId, payment.Agency, payment.Category, payment.Expense,
	                        payment.Payee, payment.DocId, payment.TransactionDate, payment.Fund, payment.SubFund,
	                        payment.Amount, transaction.UnderlyingTransaction);
	                }
	                catch (SqlException sqlE)
	                {
	                    if (sqlE.Number == 2601)
	                    {
	                        //_logger.Debug("Skipping duplicate expense...", sqlE);
	                    }
	                    else
	                    {
	                        throw;
	                    }
	                }
	                catch (Exception e)
	                {
	                    _logger.Error("Error inserting record!", e);
	                    throw;
	                }

	                i++;

	                if (i % batchSize == 0)
	                {
	                    transaction.Commit();
	                    transaction = _db.Database.BeginTransaction(IsolationLevel.ReadCommitted);
	                }
	            }

	            transaction.Commit();
	            transaction.Dispose();

	            return true;
	        }
	        catch (Exception e)
	        {
	            _logger.Error("Error writing batch!", e);
	            throw;
	        }

	    }
	}
}