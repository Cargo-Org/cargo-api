using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cargo.CustomerService.Data;

// Used ONLY by dotnet ef at design time (migrations add, database update).
// Never instantiated by the running application.
public sealed class CustomerDbContextFactory
    : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            // User secrets override appsettings.json values on the local machine.
            // optional: true — no error if user secrets are not initialised
            // (e.g. in a CI pipeline where env vars are used instead).
            .AddUserSecrets<CustomerDbContextFactory>(optional: true)
            // Environment variables override everything — used in Docker and CI.
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("CustomerDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:CustomerDb is required for migrations.");

        var optionsBuilder = new DbContextOptionsBuilder<CustomerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CustomerDbContext(optionsBuilder.Options);
    }
}