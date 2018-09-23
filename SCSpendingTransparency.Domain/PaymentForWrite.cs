using System;

namespace SCSpendingTransparency.Domain
{
    public class PaymentForWrite
    {
        public string AgencyId { get; set; }
        public string Agency { get; set; }
        public string Category { get; set; }
        public string Expense { get; set; }
        public string Payee { get; set; }
        public string Fund { get; set; }
        public string SubFund { get; set; }
        public string DocId { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
    }
}