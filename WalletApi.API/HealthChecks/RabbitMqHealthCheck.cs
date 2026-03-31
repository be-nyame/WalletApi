using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace WalletApi.API.HealthChecks;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;

    public RabbitMqHealthCheck(IConfiguration config) => _config = config;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMq:Host"],
                UserName = _config["RabbitMq:Username"],
                Password = _config["RabbitMq:Password"]
            };

            using var connection = factory.CreateConnection();
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ is reachable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("RabbitMQ is unreachable.", ex));
        }
    }
}