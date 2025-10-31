using Microsoft.EntityFrameworkCore;
using SolanaAPI_Test.Models;

namespace SolanaAPI_Test.DAL
{
    public class ApiDbContext : DbContext
    {
        public ApiDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<UserWallet> UserWallets { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasSequence<int>("UserWalletSeq", schema: "dbo")
                .StartsAt(10000)
                .IncrementsBy(1);

            modelBuilder.Entity<UserWallet>()
                .Property(u => u.UserId)
                .HasDefaultValueSql("NEXT VALUE FOR dbo.UserWalletSeq");
        }

    }
}
