using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCSpendingTransparency.Data.Models
{
	public class Payment
	{
		public Guid Id { get; set; }

		public string AgencyId { get; set; }

		[Index("IDX_AGENCY")]
		[Index("IDX_AGENCY_CATEGORY", 0)]
		[Index("IDX_AGENCY_CATEGORY_EXPENSE", 0)]
		[MaxLength(50)]
		public string Agency { get; set; }

		[Index("IDX_AGENCY_CATEGORY", 1)]
		[Index("IDX_AGENCY_CATEGORY_EXPENSE", 1)]
		[MaxLength(75)]
		public string Category { get; set; }

		[Index("IDX_AGENCY_CATEGORY_EXPENSE", 2)]
		[MaxLength(75)]
		public string Expense { get; set; }

		[Index("IDX_PAYEE")]
		[MaxLength(250)]
		public string Payee { get; set; }

		[Index("IDX_FUND_SUBFUND", 0)]
		[MaxLength(100)]
		public string Fund { get; set; }

		[MaxLength(100)]
		[Index("IDX_FUND_SUBFUND", 1)]
		public string SubFund { get; set; }
		public string DocId { get; set; }

		[Index("IDX_TRANSACTIONDATE")]
		public DateTime TransactionDate { get; set; }
		public decimal Amount { get; set; }

        public DateTimeOffset InsertedAt { get; set; }

        [Index("UK_HASH", IsUnique = true)]
        [MaxLength(128)]
        public string Hash { get; set; }
	}
}