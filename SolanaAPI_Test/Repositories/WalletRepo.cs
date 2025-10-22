using Microsoft.EntityFrameworkCore;
using SolanaAPI_Test.DAL;
using SolanaAPI_Test.Interface;
using SolanaAPI_Test.Models;
using System.Security;

namespace SolanaAPI_Test.Repositories
{
    public class WalletRepo :IWalletRepo
    {
        private readonly ApiDbContext _context;

        public WalletRepo(ApiDbContext context)
        {
            _context = context;
        }

        public async Task AddNewWallet(UserWallet wallet)
        {
            try
            {
                _context.UserWallets.Add(wallet);
                var result = await _context.SaveChangesAsync();

                if (result <= 0)
                {
                    throw new Exception("Failed to insert UserWallet into database.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inserting UserWallet: {ex.Message}", ex);
            }
        }

        public async Task<(string EncryptedDek, string EncryptedPrivateKey)> GetUserWallet(string publicKey)
        {
            var wallet = await _context.UserWallets.Where(w => w.PublicKey == publicKey).FirstOrDefaultAsync();

            if (wallet == null)
            {
                throw new Exception($"No wallet found for publicKey: {publicKey}");
            }
            return (wallet.EncryptedDek, wallet.EncryptedPrivateKey);
        }

    }
}
