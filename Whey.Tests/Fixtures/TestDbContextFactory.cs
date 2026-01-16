using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Whey.Core.Models;
using Whey.Core.Models.Stats;
using Whey.Infra.Data;

namespace Whey.Tests.Fixtures;

public class TestWheyContext : WheyContext
{
	public TestWheyContext(DbContextOptions options) : base(ChangeOptionsType(options)) { }

	private static DbContextOptions<WheyContext> ChangeOptionsType(DbContextOptions options)
	{
		// Convert DbContextOptions<TestWheyContext> to DbContextOptions<WheyContext>
		var builder = new DbContextOptionsBuilder<WheyContext>();
		builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
		builder.EnableSensitiveDataLogging();
		return builder.Options;
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		// DO NOT call base.OnModelCreating - it has PostgreSQL-specific SQL

		// Platform value converter
		var platformConverter = new ValueConverter<Platform, int>(
			v => v.GetHashCode(),
			v => GetPlatformFromHash(v));

		// Configure WheyClient
		modelBuilder.Entity<WheyClient>()
			.HasIndex(c => c.PublicKey)
			.IsUnique();

		// Configure Package
		modelBuilder.Entity<Package>(entity =>
		{
			entity.HasIndex(p => new { p.Owner, p.Repo }).IsUnique();
			entity.Ignore(p => p.Dependencies);
			entity.Ignore(p => p.ReleaseAssets);
			entity.Property(p => p.SupportedPlatforms).HasConversion(platformConverter);
		});

		// Configure PackageStatistics (simplified for InMemory)
		modelBuilder.Entity<PackageStatistics>(stats =>
		{
			stats.OwnsOne(s => s.Installs);
			stats.OwnsOne(s => s.Updates);
			// Skip TotalInteractions computed column - it's PostgreSQL-specific
			stats.Ignore(p => p.TotalInteractions);
		});
	}

	private static Platform GetPlatformFromHash(int hash)
	{
		if (hash == Platform.Linux.GetHashCode()) return Platform.Linux;
		if (hash == Platform.Windows.GetHashCode()) return Platform.Windows;
		if (hash == Platform.Darwin.GetHashCode()) return Platform.Darwin;
		return Platform.Unspecified;
	}
}

public static class TestDbContextFactory
{
	public static WheyContext Create(string? dbName = null)
	{
		dbName ??= Guid.NewGuid().ToString();

		var options = new DbContextOptionsBuilder<TestWheyContext>()
			.UseInMemoryDatabase(databaseName: dbName)
			.EnableSensitiveDataLogging()
			.Options;

		return new TestWheyContext(options);
	}
}
