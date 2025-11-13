using Microsoft.EntityFrameworkCore;
using Whey.Core.Models;

namespace Whey.Core.Data;

public class WheyContext : DbContext
{
	public DbSet<WheyClient> Clients { get; set; }
	public DbSet<Package> Packages { get; set; } // tracked packages
	public WheyContext(DbContextOptions<WheyContext> options) : base(options) { }
}
