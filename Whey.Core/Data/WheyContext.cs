using Microsoft.EntityFrameworkCore;
using Whey.Core.Models;

namespace Whey.Core.Data;

public class WheyContext : DbContext
{
	public DbSet<WheyClient> Clients { get; set; }
	public WheyContext(DbContextOptions<WheyContext> options) : base(options) { }
}
