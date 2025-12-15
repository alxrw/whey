using Microsoft.EntityFrameworkCore;
using Whey.Core.Models;
using Whey.Core.Models.Stats;

namespace Whey.Infra.Data;

public class WheyContext : DbContext
{
	public DbSet<WheyClient> Clients { get; set; }
	public DbSet<Package> Packages { get; set; } // tracked packages
	public DbSet<PackageStatistics> PackageStats { get; set; }
	public WheyContext(DbContextOptions<WheyContext> options) : base(options) { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<WheyClient>()
			.HasIndex(c => c.PublicKey)
			.IsUnique();

		modelBuilder.Entity<Package>()
			.HasIndex(p => new { p.Owner, p.Repo })
			.IsUnique();

		modelBuilder.Entity<PackageStatistics>(stats =>
		{
			stats.OwnsOne(s => s.Installs, b =>
			{
				b.ToJson();
			});

			stats.OwnsOne(s => s.Updates, b =>
			{
				b.ToJson();
			});
		});
	}
}
