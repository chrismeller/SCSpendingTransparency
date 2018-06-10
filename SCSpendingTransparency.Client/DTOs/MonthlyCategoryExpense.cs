using System;

namespace SCSpendingTransparency.Client.DTOs
{
	public class MonthlyCategoryExpense
	{
		public string Expense { get; set; }
		public Uri ExpenseUrl { get; set; }
		public decimal Amount { get; set; }

		public Agency Agency { get; set; }
		public Year Year { get; set; }
		public Month Month { get; set; }

		public MonthlyCategory Category { get; set; }

		public override string ToString()
		{
			return $"{Agency} - {Category.Category} - {Expense} - ${Amount}";
		}
	}
}