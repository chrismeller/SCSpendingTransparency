namespace SCSpendingTransparency.Client.DTOs
{
	public class SearchCriterion
	{
		public string Text { get; set; }
		public string SearchValue { get; set; }

		public override string ToString()
		{
			return $"{SearchValue} - {Text}";
		}
	}

	public class Year : SearchCriterion { }
	public class Month : SearchCriterion { }
	public class Agency : SearchCriterion { }
}