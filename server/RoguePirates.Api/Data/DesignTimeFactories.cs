using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RoguePirates.Api.Data;

// Used only by `dotnet ef` (migrations add / script / list). Adding or scripting a migration never connects, so these
// connection strings are placeholders; the running app builds its own options in Program.cs.

public sealed class PostgresGameDbDesignFactory : IDesignTimeDbContextFactory<PostgresGameDb>
{
    public PostgresGameDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresGameDb>().UseNpgsql("Host=localhost;Database=roguepirates_design").Options);
}

public sealed class SqliteGameDbDesignFactory : IDesignTimeDbContextFactory<SqliteGameDb>
{
    public SqliteGameDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteGameDb>().UseSqlite("Data Source=roguepirates-design.db").Options);
}
