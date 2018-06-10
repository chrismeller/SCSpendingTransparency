using System.Data.Entity;
using SCSpendingTransparency.Data.Models;

namespace SCSpendingTransparency.Data
{
	public class ApplicationDbContext : DbContext
	{
		public DbSet<Payment> Payments { get; set; }
	}
}