using System;
using System.Linq;
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
			//Test().ConfigureAwait(false).GetAwaiter().GetResult();
			Run().ConfigureAwait(false).GetAwaiter().GetResult();
		}

		public static async Task Run()
		{
			using (var client = new TransparencyClient())
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
										service.CreatePayment(agency.SearchValue, agency.Text, category.Category, expense.Expense, payment.Payee, payment.DocId,
											payment.TransactionDate, payment.Fund, payment.SubFund, payment.Amount);
									}

									await db.SaveChangesAsync();
								}
							}
						}
					}
				}
			}
		}

		public static async Task Test()
		{
			using (var client = new TransparencyClient())
			{
				var years = await client.GetAvailableYears();
				var months = await client.GetAvailableMonths();
				var agencies = await client.GetAvailableAgencies();

				var judicialDepartment = agencies.First(x => x.Text == "EDUCATION DEPARTMENT");
				var year2018 = years.First(x => x.Text == "2014 - 15");
				var monthApril = months.First(x => x.Text == "April");

				var categories = await client.GetMonthCategories(judicialDepartment, year2018, monthApril);

				var categoryContractualServices = categories.First(x => x.Category == "CONTRACTUAL SERVICES");

				var expenses = await client.GetMonthCategoryExpenses(categoryContractualServices);

				var expenseTraining = expenses.First(x => x.Expense == "EDUC TRNG-NON STATE");

				var payments = await client.GetMonthCategoryExpensePayments(expenseTraining);
			}
		}
	}
}
