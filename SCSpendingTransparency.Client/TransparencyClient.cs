using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using SCSpendingTransparency.Client.DTOs;

namespace SCSpendingTransparency.Client
{
	public class TransparencyClient : IDisposable
	{
		private HttpClientHandler _handler;
		private List<Year> _years;
		private List<Month> _months;
		private List<Agency> _agencies;

		private WebFormsValues _lastWebFormsValues;

		public TransparencyClient(string proxyHost = null, int proxyPort = 80, string proxyUser = null, string proxyPass = null)
		{
			_handler = new HttpClientHandler()
			{
				CookieContainer = new CookieContainer(),
				UseCookies = true,
				AllowAutoRedirect = true,
			};

		    if (!String.IsNullOrWhiteSpace(proxyHost))
		    {
		        _handler.UseProxy = true;
		        _handler.Proxy = new WebProxy()
		        {
		            Address = new Uri($"{proxyHost}:{proxyPort}"),
		        };

		        if (!String.IsNullOrWhiteSpace(proxyUser))
		        {
		            _handler.Proxy.Credentials = new NetworkCredential(proxyUser, proxyPass);
		        }
		    }
		}

		public async Task<List<Year>> GetAvailableYears()
		{
			if (_years == null) await GetSearchValues();

			return _years;
		}

		public async Task<List<Month>> GetAvailableMonths()
		{
			if (_months == null) await GetSearchValues();

			return _months;
		}

		public async Task<List<Agency>> GetAvailableAgencies()
		{
			if (_agencies == null) await GetSearchValues();

			return _agencies;
		}

		private async Task GetSearchValues()
		{
			using (var http = new HttpClient(_handler, false))
			{
				var response = await http.GetStringAsync("https://applications.sc.gov/SpendingTransparency/MonthlyExpenditureSearch.aspx?etype=1");

				var doc = new HtmlDocument();
				doc.LoadHtml(response);

				_lastWebFormsValues = ParseWebForms(doc);

				var yearsOptions =
					doc.DocumentNode.SelectNodes(
						"//select[ @id='ctl00_ContentPlaceHolder_SearchControl_YearDropdownList' ]/option");
				var monthsOptions =
					doc.DocumentNode.SelectNodes(
						"//select[ @id='ctl00_ContentPlaceHolder_SearchControl_MonthDropdownList' ]/option");
				var agencyOptions =
					doc.DocumentNode.SelectNodes(
						"//select[ @id='ctl00_ContentPlaceHolder_SearchControl_AgencyDropdownList' ]/option");

				var years = yearsOptions.Where(x => x.GetAttributeValue("value", "-1") != "-1").Select(x => new Year()
				{
					SearchValue = x.GetAttributeValue("value", "-1"),
					Text = x.InnerText,
				}).ToList();

				var months = monthsOptions.Where(x => x.GetAttributeValue("value", "-1") != "-1").Select(x => new Month()
				{
					SearchValue = x.GetAttributeValue("value", "-1"),
					Text = x.InnerText,
				}).ToList();

				var agencies = agencyOptions.Where(x => x.GetAttributeValue("value", "-1") != "-1").Select(x => new Agency()
				{
					SearchValue = x.GetAttributeValue("value", "-1"),
					Text = x.InnerText.Replace("&amp;", "&"),
				}).ToList();

				_years = years;
				_months = months;
				_agencies = agencies;
			}
		}

		public async Task<List<MonthlyCategory>> GetMonthCategories(Agency agency, Year year, Month month)
		{
			// we actually only need to get new webforms values if we've done something other than searching for categories before, but we'll do it always
			// since we've usually continued on to do something else at this point -- all this does is populate the last webforms values with the right info
			await GetSearchValues();

			using (var http = new HttpClient(_handler, false))
			{
				var postBody = new FormUrlEncodedContent(AddWebForms(new List<KeyValuePair<string, string>>()
				{
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$SearchControl$YearDropdownList", year.SearchValue),
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$SearchControl$MonthDropdownList", month.SearchValue),
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$SearchControl$AgencyDropdownList", agency.SearchValue),
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$SearchControl$SearchButton", "Search"),
				}));

				var response =
					await http.PostAsync("https://applications.sc.gov/SpendingTransparency/MonthlyExpenditureSearch.aspx?etype=1",
						postBody);
				// make sure the request was successful before we continue
				response.EnsureSuccessStatusCode();

				var responseBody = await response.Content.ReadAsStringAsync();

				var doc = new HtmlDocument();
				doc.LoadHtml(responseBody);


				// get all the table rows, excluding the first, which contains the headers
				var tableRows =
					doc.DocumentNode.SelectNodes(
						"//table[ @id='ctl00_ContentPlaceHolder_CategoryDataControl_CategoryDataTable' ]//tr[ position() > 1 ]");

				if (tableRows == null) return new List<MonthlyCategory>();

				var categories = new List<MonthlyCategory>();
				foreach (var tableRow in tableRows)
				{
					var linkNode = tableRow.SelectSingleNode(".//a");

					var categoryName = linkNode.InnerText;
					var categoryLink = linkNode.GetAttributeValue("href", "");

					var amount = tableRow.SelectSingleNode("./td[ contains( @class, 'total_highlight' ) ]").InnerText;

					var category = new MonthlyCategory()
					{
						Category = categoryName,
						CategoryUrl = new Uri(new Uri("https://applications.sc.gov/SpendingTransparency/"), categoryLink),
						Amount = Convert.ToDecimal(amount.Replace("$", "").Replace(",", "")),
						Agency = agency,
						Year = year,
						Month = month
					};

					categories.Add(category);
				}

				return categories;
			}
		}

		public async Task<List<MonthlyCategoryExpense>> GetMonthCategoryExpenses(MonthlyCategory category, Uri paginatedLink = null, bool autoPaginate = true)
		{
			// @todo compare category agency, year, month against last search value and auto-search again if needed
			using (var http = new HttpClient(_handler, false))
			{
				var url = (paginatedLink != null) ? paginatedLink : category.CategoryUrl;
				var response = await http.GetStringAsync(url);

				var doc = new HtmlDocument();
				doc.LoadHtml(response);

				_lastWebFormsValues = ParseWebForms(doc);

				var paginationNode = doc.DocumentNode.SelectSingleNode("//p[ contains( @class, 'paginationNote' ) ]");
				var paginationRegex = new Regex(@"Displaying records (\d+) through (\d+) of (\d+) found.");

				var nextNode =
					doc.DocumentNode.SelectSingleNode("//div[ contains( @class, 'nextBack' ) ]/a[ contains( text(), 'Next' ) ]");

				var expenseRows =
					doc.DocumentNode.SelectNodes(
						"//table[ @id='ctl00_ContentPlaceHolder_ObjectDataControl_CategoryDataTable' ]//tr[ position() > 1 ]");

				var expenses = new List<MonthlyCategoryExpense>();
				foreach (var expenseRow in expenseRows)
				{
					var linkNode = expenseRow.SelectSingleNode(".//a");

					var expenseName = linkNode.InnerText;
					var expenseLink = linkNode.GetAttributeValue("href", "");

					var amount = expenseRow.SelectSingleNode("./td[ contains( @class, 'total_highlight' ) ]").InnerText;

					var expense = new MonthlyCategoryExpense()
					{
						Expense = expenseName,
						ExpenseUrl = new Uri(new Uri("https://applications.sc.gov/SpendingTransparency/"), expenseLink),
						Amount = Convert.ToDecimal(amount.Replace("$", "").Replace(",", "")),
						Agency = category.Agency,
						Year = category.Year,
						Month = category.Month,
						Category = category
					};

					expenses.Add(expense);
				}

				// if there's another page of expenses, add those recursively - only if we haven't disabled paginating
				if (nextNode != null && autoPaginate)
				{
					var nextLink = new Uri(new Uri("https://applications.sc.gov/SpendingTransparency/"),
						nextNode.GetAttributeValue("href", ""));

					expenses.AddRange(await GetMonthCategoryExpenses(category, nextLink));
				}

				return expenses;
			}
		}

		public async Task<List<MonthlyCategoryExpensePayment>> GetMonthCategoryExpensePayments(MonthlyCategoryExpense expense)
		{
			using (var http = new HttpClient(_handler, false))
			{
				// we have to first fetch the initial page to get webforms values
				var getResponse = await http.GetAsync(expense.ExpenseUrl);
				getResponse.EnsureSuccessStatusCode();

				var getResponseContent = await getResponse.Content.ReadAsStringAsync();

				var doc = new HtmlDocument();
				doc.LoadHtml(getResponseContent);

				_lastWebFormsValues = ParseWebForms(doc);

				// then we can add those into the POST request and get everything as a CSV
				var postValues = AddWebForms(new List<KeyValuePair<string, string>>()
				{
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$PayeeControl$SortDropDownList", "0"),
					new KeyValuePair<string, string>("ctl00$ContentPlaceHolder$PayeeControl$ExportButton", "Download .CSV file"),
				});
				var postBody = new FormUrlEncodedContent(postValues);

				var response = await http.PostAsync(expense.ExpenseUrl, postBody);
				var responseBody = await response.Content.ReadAsStringAsync();

				// trim the first 6 lines, they are header crap
				var lines = responseBody.Split(new []{ '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				var csvContents = String.Join("\r\n", lines.Reverse().Take(lines.Length - 6).Reverse());

				using (var stream = new StringReader(csvContents))
				{
					var csv = new CsvReader(stream);
					csv.Configuration.TrimOptions = TrimOptions.Trim | TrimOptions.InsideQuotes;
					csv.Configuration.RegisterClassMap<ExpensePaymentClassMap>();
                    // looks like sometimes subfund can be "SubFund Title" and others "SubFund_Title" - fix that
				    csv.Configuration.PrepareHeaderForMatch = header => header.Replace("_", " ");

					return csv.GetRecords<MonthlyCategoryExpensePayment>().ToList();
				}
			}
		}

		private WebFormsValues ParseWebForms(HtmlDocument doc)
		{
			return new WebFormsValues()
			{
				EventValidation = doc.DocumentNode.SelectSingleNode("//input[ @id='__EVENTVALIDATION' ]")?
					.GetAttributeValue("value", ""),
				ViewState = doc.DocumentNode.SelectSingleNode("//input[ @id='__VIEWSTATE' ]")?.GetAttributeValue("value", ""),
				ViewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[ @id='__VIEWSTATEGENERATOR' ]")?
					.GetAttributeValue("value", ""),
			};
		}

		private List<KeyValuePair<string, string>> AddWebForms(List<KeyValuePair<string, string>> values)
		{
			values.Add(new KeyValuePair<string, string>("__VIEWSTATE", _lastWebFormsValues.ViewState));
			values.Add(new KeyValuePair<string, string>("__VIEWSTATEGENERATOR", _lastWebFormsValues.ViewStateGenerator));
			values.Add(new KeyValuePair<string, string>("__EVENTVALIDATION", _lastWebFormsValues.EventValidation));

			return values;
		}

		public void Dispose()
		{
			_handler = null;
		}

		private class WebFormsValues
		{
			public string ViewState { get; set; }
			public string EventValidation { get; set; }
			public string ViewStateGenerator { get; set; }
		}

		public sealed class ExpensePaymentClassMap : ClassMap<MonthlyCategoryExpensePayment>
		{
			public ExpensePaymentClassMap()
			{
				AutoMap();

				Map(m => m.DocId).Name("Doc ID");
				Map(m => m.TransactionDate).Name("Transaction Date");
				Map(m => m.Fund).Name("Fund Title");
				Map(m => m.SubFund).Name("SubFund_Title", "SubFund Title");
				Map(m => m.Amount).ConvertUsing(row => Convert.ToDecimal(row.GetField("Amount").Replace("$", "").Replace(",", "")));
			}
		}
	}
}