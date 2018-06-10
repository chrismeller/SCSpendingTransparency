using System;

namespace SCSpendingTransparency.Client.DTOs
{
	public class MonthlyCategoryExpensePayment
	{
		public string Payee { get; set; }
		public string DocId { get; set; }
		public DateTime TransactionDate { get; set; }
		public string Fund { get; set; }
		public string SubFund { get; set; }
		public decimal Amount { get; set; }
	}
}