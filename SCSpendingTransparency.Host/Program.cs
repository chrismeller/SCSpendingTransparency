using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using NLog;
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
		    Console.WriteLine("Done!");
		    Console.ReadLine();
		}

		public static async Task Run()
		{
		    var logger = LogManager.GetLogger("Default");

		    logger.Info("Beginning execution.");

            var proxyHost = ConfigurationManager.AppSettings.Get("Proxy.Host");
		    var proxyPort = (String.IsNullOrWhiteSpace(ConfigurationManager.AppSettings.Get("Proxy.Port")))
		        ? 80
		        : Convert.ToInt32(ConfigurationManager.AppSettings.Get("Proxy.Port"));
		    var proxyUser = ConfigurationManager.AppSettings.Get("Proxy.User");
		    var proxyPass = ConfigurationManager.AppSettings.Get("Proxy.Pass");

		    logger.Debug("Using configured proxy server {0}", proxyHost);

			using (var client = new TransparencyClient(proxyHost, proxyPort, proxyUser, proxyPass))
			using (var db = new ApplicationDbContext())
			{
				var service = new PaymentService(db, logger);

				var years = await client.GetAvailableYears();
				var months = await client.GetAvailableMonths();
				var agencies = await client.GetAvailableAgencies();

			    var resume = false;
				foreach (var year in years.AsEnumerable().Reverse())
				{
				    logger.Info("Executing year {0}", year.SearchValue);

					foreach (var month in months.AsEnumerable().Reverse())
					{
					    logger.Info("Executing month {0} for year {1}", month.SearchValue, year.SearchValue);

                        // tracks whether or not we have found data for any agencies so far in this month
					    var haveGottenDataForMonth = false;

                        // if haveGottenDataForMonth is false by the time we get to this agency in the list, we'll skip the rest
					    var emtpyAgenciesInMonthThreshold = 5;

						foreach (var agency in agencies)
						{
						    if (year.SearchValue == "2017" && month.SearchValue == "5" &&
						        agency.Text.ToLower().Contains("dept of natural"))
						    {
						        resume = true;
						    }

						    if (resume == false)
						    {
                                logger.Info("Skipping for resume {0} for {1}-{2}", agency.Text, year.Text, month.Text);
						        continue;
						    }

						    logger.Info("Executing agency {0} for {1}-{2}", agency.SearchValue, year.SearchValue,
						        month.SearchValue);

                            //if (haveGottenDataForMonth == false &&
                            //    agencies.IndexOf(agency) > emtpyAgenciesInMonthThreshold)
                            //{
                            //    logger.Debug("Threshold reached, skipping {0} for {1}-{2}", agency.Text, year.Text,
                            //        month.Text);
                            //    continue;
                            //}

                            logger.Info("Fetching {0} for {1}-{2} ", agency.Text, year.Text, month.Text);

							var categories = await client.GetMonthCategories(agency, year, month);

                            logger.Info("{0} categories", categories.Count);

						    if (categories.Count > 0)
						    {
						        haveGottenDataForMonth = true;
						    }

							foreach (var category in categories)
							{
								logger.Info("\tCategory {0} ", category.Category);

								var expenses = await client.GetMonthCategoryExpenses(category);

								logger.Info("{0} expenses", expenses.Count);

								foreach (var expense in expenses)
								{
									logger.Info("\t\tExpense {0} ", expense.Expense);

									var payments = await client.GetMonthCategoryExpensePayments(expense);

								    logger.Info("{0} payments", payments.Count);

								    var forWrite = payments.Select(x => new PaymentForWrite()
								    {
                                        Agency = agency.Text.Trim(),
                                        AgencyId = agency.SearchValue,
                                        Amount = x.Amount,
                                        Category = category.Category.Trim(),
                                        DocId = x.DocId,
                                        Expense = expense.Expense.Trim(),
                                        Fund = x.Fund.Trim(),
                                        Payee = x.Payee.Trim(),
                                        SubFund = x.SubFund.Trim(),
                                        TransactionDate = x.TransactionDate,
								    });

								    logger.Debug("Writing batch of payments.");

								    await service.CreatePaymentBatch(forWrite);

								    logger.Debug("Batch complete.");

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
