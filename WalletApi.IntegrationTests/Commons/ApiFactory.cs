using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using WalletApi.Infrastructure.Data;

namespace WalletApi.IntegrationTests.Commons;

/// <summary>
/// A single <see cref="WebApplicationFactory{TEntryPoint}"/> shared across
/// all auth tests. Each test calls <see cref="ResetDatabaseAsync"/> to
/// guarantee a clean slate without the overhead of rebuilding the host.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var internalServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<WalletDbContext>(options =>
            {
                options.UseInMemoryDatabase("AuthTestDb");
                options.UseInternalServiceProvider(internalServiceProvider);
            });
        });
    }
}