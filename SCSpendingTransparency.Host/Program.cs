using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
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
					    var emptyAgenciesInMonth = 0;
					    var emtpyAgenciesInMonthThreshold = 5;

						foreach (var agency in agencies)
						{
						    if (emptyAgenciesInMonth >= emtpyAgenciesInMonthThreshold)
						    {
                                Console.WriteLine("Threshold reached, skipping {0} for {1}-{2}", agency.Text, year.Text, month.Text);
						        continue;
						    }

                            Console.Write("Fetching {0} for {1}-{2} ", agency.Text, year.Text, month.Text);

							var categories = await client.GetMonthCategories(agency, year, month);

                            Console.WriteLine("{0} categories", categories.Count);

						    if (categories.Count == 0)
						    {
						        emptyAgenciesInMonth++;
						    }

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

                                    using (var scope = new TransactionScope(TransactionScopeOption.Required,
                                        new TransactionOptions
                                        {
                                            IsolationLevel = IsolationLevel.ReadCommitted,
                                            Timeout = TransactionManager.MaximumTimeout,
                                        }))
                                    {
                                        foreach (var payment in payments)
								        {
								            try
								            {
								                service.CreatePayment(agency.SearchValue, agency.Text.Trim(),
								                    category.Category.Trim(), expense.Expense.Trim(),
								                    payment.Payee.Trim(),
								                    payment.DocId,
								                    payment.TransactionDate, payment.Fund.Trim(),
								                    payment.SubFund.Trim(),
								                    payment.Amount);
								            }
								            catch (SqlException sqlE)
								            {
								                if (sqlE.Number == 2601)
								                {
								                    Console.WriteLine("Skipping duplicate expense...");
								                }
								                else
								                {
								                    throw;
								                }
								            }
								            catch (Exception e)
								            {
								                Console.WriteLine("Error inserting record!");
								                throw;
								            }
								        }

								        scope.Complete();
								    }

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
