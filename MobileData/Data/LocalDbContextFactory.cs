using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MobileData.Data
{
    public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
    {
        public LocalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();

            var tempPath = Path.Combine(Path.GetTempPath(), "AssetTagDesignTime.db3");
            optionsBuilder.UseSqlite($"Data Source={tempPath}");

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
#endif

            // Use temp path for design-time
            return new LocalDbContext(optionsBuilder.Options, tempPath);
        }
    }
}
