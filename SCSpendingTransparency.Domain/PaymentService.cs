using System;
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

		public bool CreatePayment(string agencyId, string agency, string category, string expense, string payee, string docId, DateTime transactionDate, string fund, string subFund, decimal amount)
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

            var result = _db.Database.Connection.Execute(@"
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
)", payment);

		    if (result == 1)
		    {
		        return true;
		    }
		    else
		    {
		        throw new Exception("Error inserting Payment record!");
		    }
		}
	}
}