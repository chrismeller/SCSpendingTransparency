using System;
using System.Threading.Tasks;
using SCSpendingTransparency.Data;
using SCSpendingTransparency.Data.Models;

namespace SCSpendingTransparency.Domain
{
	public class PaymentService
	{
		private readonly ApplicationDbContext _db;

		public PaymentService(ApplicationDbContext db)
		{
			_db = db;
		}

		public void CreatePayment(string agencyId, string agency, string category, string expense, string payee, string docId, DateTime transactionDate, string fund, string subFund, decimal amount)
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

			_db.Payments.Add(payment);
		}
	}
}