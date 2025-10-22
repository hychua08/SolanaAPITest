using SolanaAPI_Test.Models;

namespace SolanaAPI_Test.Interface
{
    public interface IWalletRepo
    {
        Task AddNewWallet(UserWallet wallet);
        Task<(string EncryptedDek, string EncryptedPrivateKey)> GetUserWallet(string publicKey);
    }
}
