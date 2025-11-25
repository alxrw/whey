using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Whey.Infra.Data;

namespace Whey.Infra.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddWheyInfra(this IServiceCollection services, IConfiguration config)
	{
		var connectionString = config.GetConnectionString("DefaultConnection"); // ??

		services.AddDbContext<WheyContext>(opts =>
		{
			opts.UseNpgsql(connectionString, npgsqlOpts =>
			{
				// opts here
			});
		});

		return services;
	}
}
