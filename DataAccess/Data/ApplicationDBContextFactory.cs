using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAccess.Data
{
    public class ApplicationDBContextFactory : IDesignTimeDbContextFactory<ApplicationDBContext>
    {
        public ApplicationDBContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDBContext>();
            
            // Ganti dengan path yang sesuai untuk database Anda
            optionsBuilder.UseSqlite("Data Source=../database.db");
            
            return new ApplicationDBContext(optionsBuilder.Options);
        }
    }
}