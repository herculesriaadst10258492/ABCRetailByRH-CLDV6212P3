using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace ABCRetailByRH.Data
{
    // Lets 'Add-Migration' run without executing Program.cs
    public class DesignTimeAuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var conn = config.GetConnectionString("AuthDatabase");

            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(conn)
                .Options;

            return new AuthDbContext(options);
        }
    }
}
