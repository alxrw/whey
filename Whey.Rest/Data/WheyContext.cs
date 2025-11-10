using Microsoft.EntityFrameworkCore;
using Whey.Rest.Models;

namespace Whey.Rest.Data;

public class WheyContext : DbContext
{
	public DbSet<WheyClient> Clients { get; set; }
	public WheyContext(DbContextOptions<WheyContext> options) : base(options) { }
}
