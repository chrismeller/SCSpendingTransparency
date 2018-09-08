using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SCSpendingTransparency.Client;
using SCSpendingTransparency.Data;
using SCSpendingTransparency.Domain;

namespace SCSpendingTransparency.Host
{
	class Program
	{
		static void Main(string[] args)
		{
			Run().ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public static async Task Run()
		{
		    var proxyHost = ConfigurationManager.AppSettings.Get("Proxy.Host");
		    var proxyPort = (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings.Get("Proxy.Port")))
		        ? 80
		        : Convert.ToInt32(ConfigurationManager.AppSettings.Get("Proxy.Port"));
		    var proxyUser = ConfigurationManager.AppSettings.Get("Proxy.User");
		    var proxyPass = ConfigurationManager.AppSettings.Get("Proxy.Pass");

			using (var client = new TransparencyClient(proxyHost, proxyPort, proxyUser, proxyPass))
			using (var db = new ApplicationDbContext())
			{
				var service = new PaymentService(db);

				var years = await client.GetAvailableYears();
				var months = await client.GetAvailableMonths();
				var agencies = await client.GetAvailableAgencies();

				foreach (var year in years.AsEnumerable().Reverse())
				{
					foreach (var month in months.AsEnumerable().Reverse())
					{
						foreach (var agency in agencies)
						{
							Console.WriteLine("Fetching {0} for {1}-{2}", agency.Text, year.Text, month.Text);

							var categories = await client.GetMonthCategories(agency, year, month);

							foreach (var category in categories)
							{
								Console.Write("\tCategory {0} ", category.Category);

								var expenses = await client.GetMonthCategoryExpenses(category);

								Console.WriteLine("{0} expenses", expenses.Count);

								foreach (var expense in expenses)
								{
									Console.Write("\t\tExpense {0} ", expense.Expense);

									var payments = await client.GetMonthCategoryExpensePayments(expense);

									Console.WriteLine("{0} payments", payments.Count);

									foreach (var payment in payments)
									{
										service.CreatePayment(agency.SearchValue, agency.Text.Trim(), category.Category.Trim(), expense.Expense.Trim(), payment.Payee.Trim(), payment.DocId,
											payment.TransactionDate, payment.Fund.Trim(), payment.SubFund.Trim(), payment.Amount);
									}

									await db.SaveChangesAsync();

									// we sleep for a quarter of a second after getting each payment export
									Thread.Sleep(250);
								}

								// we also sleep for a quarter of a second after each category
								Thread.Sleep(250);
							}

							// and finally we sleep another quarter of a second after each agency
							Thread.Sleep(250);
						}
					}
				}
			}
		}
	}
}
