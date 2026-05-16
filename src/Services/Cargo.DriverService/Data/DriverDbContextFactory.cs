using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cargo.DriverService.Data;

// Used ONLY by dotnet ef at design time (migrations add, database update).
// Never instantiated by the running application.
public sealed class DriverDbContextFactory
    : IDesignTimeDbContextFactory<DriverDbContext>
{
    public DriverDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            // User secrets override appsettings.json values on the local machine.
            // optional: true — no error if user secrets are not initialised
            // (e.g. in a CI pipeline where env vars are used instead).
            .AddUserSecrets<DriverDbContextFactory>(optional: true)
            // Environment variables override everything — used in Docker and CI.
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DriverDb")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DriverDb is required for migrations.");

        var optionsBuilder = new DbContextOptionsBuilder<DriverDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new DriverDbContext(optionsBuilder.Options);
    }
}
