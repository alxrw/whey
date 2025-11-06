using Microsoft.EntityFrameworkCore;
using Whey.Models;

namespace Whey.Data;

public class WheyContext : DbContext
{
	public DbSet<WheyClient> Clients { get; set; }
	public WheyContext(DbContextOptions<WheyContext> options) : base(options) { }
}
