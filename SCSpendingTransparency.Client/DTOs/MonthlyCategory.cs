using System;

namespace SCSpendingTransparency.Client.DTOs
{
	public class MonthlyCategory
	{
		public string Category { get; set; }
		public Uri CategoryUrl { get; set; }
		public decimal Amount { get; set; }

		public Agency Agency { get; set; }
		public Year Year { get; set; }
		public Month Month { get; set; }

		public override string ToString()
		{
			return $"{Agency} - {Category} - ${Amount}";
		}
	}
}