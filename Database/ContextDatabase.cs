using System;
using Microsoft.EntityFrameworkCore;

namespace Database
{
    public class ContextDatabase: DbContext
    {

        public DbSet<Image> Images { get; set; }

        public ContextDatabase()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options) => options
            .UseLazyLoadingProxies()
            .UseSqlServer($"Data Source=(localdb)\\MSSQLLocalDB; Initial Catalog = Imagesdb.db; Integrated Security = True");
    }
}
