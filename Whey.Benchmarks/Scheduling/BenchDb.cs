using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Whey.Core.Models;
using Whey.Core.Models.Stats;
using Whey.Infra.Data;

namespace Whey.Benchmarks.Scheduling;

// WheyContext minus the Postgres-only column types, so the real ApiSchedulingService can run against
// a real SQL engine (SQLite) with the same indexes. EF InMemory would hide query cost entirely.
public sealed class SqliteWheyContext : WheyContext
{
	public SqliteWheyContext(DbContextOptions<WheyContext> options) : base(options) { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<WheyClient>().HasIndex(c => c.PublicKey).IsUnique();

		modelBuilder.Entity<Package>(e =>
		{
			e.HasIndex(p => new { p.Owner, p.Repo }).IsUnique();
			e.Ignore(p => p.Dependencies);
			e.Ignore(p => p.ReleaseAssets);
		});

		modelBuilder.Entity<PackageStatistics>(s =>
		{
			s.Property(x => x.Installs).HasConversion(
				v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
				v => JsonSerializer.Deserialize<InstallStat>(v, (JsonSerializerOptions?)null)!);
			s.HasIndex(p => p.TotalInteractions);
		});
	}

	public static SqliteWheyContext Create(string dbPath)
	{
		var opts = new DbContextOptionsBuilder<WheyContext>()
			.UseSqlite(new SqliteConnectionStringBuilder { DataSource = dbPath, Cache = SqliteCacheMode.Shared }.ToString())
			.Options;
		var ctx = new SqliteWheyContext(opts);
		ctx.Database.EnsureDeleted();
		ctx.Database.EnsureCreated();
		return ctx;
	}

	// Inserts packages with the given interaction counts and last-release ages (days before now).
	public void Seed(long[] interactions, double[] ageDays)
	{
		ChangeTracker.AutoDetectChangesEnabled = false;
		const int batch = 2000;
		for (int start = 0; start < interactions.Length; start += batch)
		{
			int end = Math.Min(start + batch, interactions.Length);
			for (int i = start; i < end; i++)
			{
				var id = Guid.CreateVersion7();
				Packages.Add(new Package
				{
					Id = id,
					Owner = "owner" + i,
					Repo = "repo" + i,
					Version = "v1",
					LastReleased = DateTimeOffset.UtcNow.AddDays(-ageDays[i]),
				});
				var stats = new PackageStatistics { Id = id, PackageId = id, TotalInteractions = interactions[i] };
				stats.Installs.Track();
				PackageStats.Add(stats);
			}
			SaveChanges();
			ChangeTracker.Clear();
		}
	}
}
