using System.Text.Json;
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

		modelBuilder.Entity<Package>(entity =>
		{
			entity.HasIndex(p => new { p.Owner, p.Repo }).IsUnique();

			entity.Property(p => p.Dependencies).HasColumnType("jsonb");
			entity.Property(p => p.ReleaseAssets).HasColumnType("jsonb");
			entity.Property(p => p.SupportedPlatforms).HasColumnType("integer");
		});

		modelBuilder.Entity<PackageStatistics>(stats =>
		{
			stats.Property(s => s.Installs)
				.HasColumnType("jsonb")
				.HasConversion(
					v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
					v => JsonSerializer.Deserialize<InstallStat>(v, (JsonSerializerOptions?)null)!
				);

			stats.HasIndex(p => p.TotalInteractions);
		});
	}
}
