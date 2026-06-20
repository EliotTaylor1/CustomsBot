using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomsBot.Data;

/// <summary>
/// Used only by the EF Core tools (migrations) so they can build the model without the
/// Aspire host wiring up a real connection. At runtime the connection comes from DI.
/// </summary>
public class CustomsBotDbContextFactory : IDesignTimeDbContextFactory<CustomsBotDbContext>
{
    public CustomsBotDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CustomsBotDbContext>()
            .UseNpgsql("Host=localhost;Database=customsbot;Username=postgres;Password=postgres")
            .Options;
        return new CustomsBotDbContext(options);
    }
}
