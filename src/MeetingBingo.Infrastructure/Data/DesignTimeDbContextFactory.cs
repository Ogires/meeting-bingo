using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MeetingBingo.Infrastructure.Data;

/// <summary>
/// Lets "dotnet ef" build the model without starting the API. Adding a migration or generating a script
/// never connects, so the connection string is a placeholder, not a real server.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BingoDbContext>
{
    public BingoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BingoDbContext>()
            .UseSqlServer("Server=localhost;Database=bingo;Integrated Security=true;TrustServerCertificate=true")
            .Options);
}
