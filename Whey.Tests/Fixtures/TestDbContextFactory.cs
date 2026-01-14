using Microsoft.EntityFrameworkCore;
using Whey.Infra.Data;

namespace Whey.Tests.Fixtures;

public static class TestDbContextFactory
{
	public static WheyContext Create(string? dbName = null)
	{
		dbName ??= Guid.NewGuid().ToString();

		var options = new DbContextOptionsBuilder<WheyContext>()
			.UseInMemoryDatabase(databaseName: dbName)
			.Options;

		var context = new WheyContext(options);
		context.Database.EnsureCreated();

		return context;
	}
}
