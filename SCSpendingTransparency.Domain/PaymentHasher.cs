using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SCSpendingTransparency.Data.Models;

namespace SCSpendingTransparency.Domain
{
    public class PaymentHasher
    {
        public static string Hash(Payment payment)
        {
            var pieces = new List<string>()
            {
                payment.Agency,
                payment.Category,
                payment.Expense,
                payment.Payee,
                payment.Fund,
                payment.SubFund,
                payment.DocId,
                payment.Amount.ToString(),
                payment.TransactionDate.ToString("O"),
            };

            var syntheticKey = String.Join("|", pieces);

            return Sha256(syntheticKey);
        }

        private static string Sha256(string input)
        {
            var sha = SHA256.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);

            var hashBytes = sha.ComputeHash(inputBytes);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}