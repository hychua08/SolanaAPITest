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

    }
}
