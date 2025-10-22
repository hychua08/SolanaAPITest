using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using SolanaAPI_Test.Helpers;
using SolanaAPI_Test.Interface;
using SolanaAPI_Test.Models;
using Solnet.KeyStore;
using Solnet.KeyStore.Services;
using Solnet.Programs;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Wallet;
using Solnet.Wallet.Bip39;
using Solnet.Wallet.Utilities;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace SolanaAPI_Test.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SolWalletController(IWalletRepo walletrepo) : ControllerBase
    {

        private const decimal LamportsPerSol = 1_000_000_000m;

        private IRpcClient GetRpcClient(string cluster)
        {
            return cluster?.ToLower() switch
            {
                "devnet" => ClientFactory.GetClient(Cluster.DevNet),
                "testnet" => ClientFactory.GetClient(Cluster.TestNet),
                _ => ClientFactory.GetClient(Cluster.MainNet) // default to mainnet
            };
        }

        [HttpGet("new_wallet")]
        public async Task<IActionResult> CreateWallet([FromQuery] string password = "")
        {
            var mnemonic = new Mnemonic(WordList.English, WordCount.Twelve);
            var wallet = new Wallet(mnemonic, password);

            var account = wallet.GetAccount(0);

            /**
             * step to store wallet detail
            1.generate acc
            2.generate 32byte dek
            3.encrypt private key with dek
            4.encrypt dek using aws kms service
            5 store encrtypreed dek and encrypted private key to database
            **/

            byte[] dek = RandomNumberGenerator.GetBytes(32);
            byte[] privateKeyBytes = account.PrivateKey.KeyBytes;
            var encryptedPrivateKey = AesGcmHelpers.EncryptToBase64(dek, privateKeyBytes); //To store

            var kmsHelper = new KmsHelper();
            byte[] encryptedDek = await kmsHelper.KmsWrapKey(dek); 
            var strEncryptedDek = Convert.ToBase64String(encryptedDek);// To store

            /**
             * Generate keystore for user download/backup
            **/
            var secretKeyStoreService = new SecretKeyStoreService();
            var keystore = secretKeyStoreService.EncryptAndGenerateDefaultKeyStoreAsJson(password, account.PrivateKey, account.PublicKey);
            string walletDir = Path.Combine(Directory.GetCurrentDirectory(), "wallets");
            Directory.CreateDirectory(walletDir);
            string keystorePath = Path.Combine(walletDir, $"{account.PublicKey}.json");
            System.IO.File.WriteAllText(keystorePath, (keystore));

            var walletToStore = new UserWallet
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                PublicKey = account.PublicKey.ToString(),
                KeystoreJson = keystore,
                CryptoAlgorithm = "AWS KMS",
                EncryptedDek = strEncryptedDek,
                EncryptedPrivateKey = encryptedPrivateKey,
                Metadata = ""
            };

            await walletrepo.AddNewWallet(walletToStore);

            var result = new
            {
                Mnemonic = mnemonic.ToString(),
                PublicKey = account.PublicKey,
                keystore = keystore
            };


            return Ok(result);
        }

        [HttpGet("get_balance")]
        public async Task<IActionResult> GetBalance(string publicKey, [FromQuery] string cluster = "devnet")
        {
            if (string.IsNullOrEmpty(publicKey))
            {
                return BadRequest("publicKey Required");
            }

            var rpc = GetRpcClient(cluster);
            if (!Solnet.Wallet.PublicKey.IsValid(publicKey))
            {
                return BadRequest("invalid address");
            }

            try
            {
                var pk = new Solnet.Wallet.PublicKey(publicKey);
                var balanceResp = await rpc.GetBalanceAsync(pk);
                if (!balanceResp.WasSuccessful)
                {
                    return StatusCode(502, new { error = "rpc error" });
                }

                var lamports = balanceResp.Result.Value;
                var sol = (decimal)lamports / LamportsPerSol;
                var example = balanceResp.RawRpcResponse.ToLower();
                return Ok(new
                {
                    publicKey = publicKey,
                    cluster = cluster,
                    lamports = lamports,
                    sol = sol,
                    example = example
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
        {
            try
            {
                var privateKey = "P2phd8jonKV778MBi9mAkeTtQ8SkinFsKtUXWe3pAdrLJpHdtpeMURrXrNBZ44m8v7P8RDEKTUVJkBiiorsbW8K";
                var publicKey = "91X2iNqbXxqm7Jtnn7zrCxYYpo3W8RvGatV1k8j17FsH";
                var sender = new Account(privateKey, publicKey);

                var rpc = GetRpcClient("devnet");
                var balanceResponse = await rpc.GetBalanceAsync(sender.PublicKey);
                decimal balance = balanceResponse.Result.Value / LamportsPerSol;

                if(balance < request.Amount)
                {
                    return BadRequest("Insufficient balance");
                }

                var blockhash = await rpc.GetLatestBlockHashAsync();

                ulong lamports = (ulong)(request.Amount * LamportsPerSol);
                var recipient = new PublicKey(request.Recipient);

                var tx = new TransactionBuilder().SetRecentBlockHash(blockhash.Result.Value.Blockhash)
                    .SetFeePayer(sender)
                    .AddInstruction(Solnet.Programs.SystemProgram.Transfer(
                        fromPublicKey: sender.PublicKey,
                        toPublicKey: recipient,
                        lamports:lamports
                        )
                    )
                    .Build(sender);

                var sendTx = await rpc.SendTransactionAsync(tx);

                if (sendTx.WasSuccessful)
                {
                    return Ok(new
                    {
                        message = "Transaction sent successfully",
                        signature = sendTx.Result,
                        sender = sender.PublicKey,
                        recipient = request.Recipient,
                        amount = request.Amount
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        message = "Transaction failed",
                        error = sendTx.Reason
                    });
                }
            }
            catch (Exception ex) 
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("transferbypk")]
        public async Task<IActionResult> TransferByPublicKey([FromBody] TransferRequest request)
        {
            try 
            { 
                string enDek; 
                string enPk; 
                var walletDetail = await walletrepo.GetUserWallet(request.sender); 
                enDek = walletDetail.EncryptedDek; 
                enPk = walletDetail.EncryptedPrivateKey; 

                /* Decrypt step: 
                 * 1.Get Encypted Data from db 
                 * 2.Decrypt Dek using aws kms 
                 * 3.Decypte Private Key 
                 */ 

                byte[] encryptedDekBytes = Convert.FromBase64String(enDek);
                var kmsHelper = new KmsHelper(); 
                byte[] deDek = await kmsHelper.KmsUnwrapKey(encryptedDekBytes); 

                byte[] decryptedPrivateKey = AesGcmHelpers.DecryptFromBase64(deDek, enPk); 

                //4
                var privateKey = Convert.ToBase64String(decryptedPrivateKey);
                byte[] privateKeyBytes = Convert.FromBase64String(privateKey);
                byte[] publicKeyBytes = Convert.FromBase64String(request.sender);
                string publicKeyBase64 = request.sender;
                var privateKeyBase64 = Convert.ToBase64String(decryptedPrivateKey);
                //var sender = new Account(decryptedPrivateKey, publicKeyBytes);
                var sender = new Account(privateKeyBase64, publicKeyBase64);
                var rpc = GetRpcClient("devnet");

                var balanceResponse = await rpc.GetBalanceAsync(sender.PublicKey); 
                //decimal balance = balanceResponse.Result.Value / LamportsPerSol; 

                //if (balance < request.Amount) 
                //{ 
                //    return BadRequest("Insufficient balance");
                //} 
                var blockhash = await rpc.GetLatestBlockHashAsync(); 

                ulong lamports = (ulong)(request.Amount * LamportsPerSol);

                var recipient = new PublicKey(request.Recipient); 
                var tx = new TransactionBuilder()
                    .SetRecentBlockHash(blockhash.Result.Value.Blockhash)
                    .SetFeePayer(sender)
                    .AddInstruction(Solnet.Programs.SystemProgram.Transfer
                        (fromPublicKey: sender.PublicKey, 
                        toPublicKey: recipient, 
                        lamports: lamports))
                    .Build(sender); 
                
                var sendTx = await rpc.SendTransactionAsync(tx); 
                if (sendTx.WasSuccessful) 
                { 
                    return Ok(new 
                    { message = "Transaction sent successfully", 
                        signature = sendTx.Result, 
                        sender = sender.PublicKey, 
                        recipient = request.Recipient, 
                        amount = request.Amount }); 
                } 
                else { 
                    return BadRequest(new 
                    { message = "Transaction failed", 
                        error = sendTx.Reason }
                    ); 
                } 
                }
            catch (Exception ex) 
            { 
                return BadRequest(new { error = ex.Message }); 
            }

         }

        [HttpPost("airdrop")]
        public async Task<IActionResult> RequestAirdrop(TransferRequest request)
        {
            try
            {
                var rpc = GetRpcClient("devnet");
                var publicKey = new PublicKey(request.Recipient);

                ulong lamports = (ulong)(request.Amount * LamportsPerSol);
                var tx = await rpc.RequestAirdropAsync(publicKey, lamports);

                return Ok(new
                {
                    message = "Airdrop requested",
                    signature = tx.Result,
                    amount = request.Amount,
                    address = request.Recipient
                });
            }
            catch(Exception ex) 
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("createtoken")]
        public async Task<IActionResult> CreateToken(string minAuthPublicKey)
        {
            byte[] deDek = [];
            byte[] decryptedPrivateKey = [];

            string enDek;
            string enPrivateKey;

            var walletDetail = await walletrepo.GetUserWallet(minAuthPublicKey);

            enDek = walletDetail.EncryptedDek;
            enPrivateKey = walletDetail.EncryptedPrivateKey;

            byte[] dek = Convert.FromBase64String(enDek);
            var kmsHelper = new KmsHelper();
            deDek = await kmsHelper.KmsUnwrapKey(dek);

            decryptedPrivateKey = AesGcmHelpers.DecryptFromBase64(deDek, enPrivateKey);
            byte[] publicKey = Convert.FromBase64String(minAuthPublicKey);

            Account mintAuth = new Account(decryptedPrivateKey, publicKey);

            var rpc = GetRpcClient("devnet");
            var blockhash = (await rpc.GetLatestBlockHashAsync()).Result.Value.Blockhash;

            var mintAccount = new Account();//create mint acc

            var tokenProgramId = TokenProgram.ProgramIdKey;
            var rentExemption = await rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);

            //create mint
            var tx = new TransactionBuilder()
                .SetFeePayer(mintAuth)
                .SetRecentBlockHash(blockhash)
                .AddInstruction(SystemProgram.CreateAccount(
                    mintAuth.PublicKey,
                    mintAccount.PublicKey,
                    rentExemption.Result,
                    TokenProgram.MintAccountDataSize,
                    tokenProgramId))
                .AddInstruction(TokenProgram.InitializeMint(
                    mintAccount.PublicKey,
                    9,
                    mintAuth.PublicKey,
                    mintAuth.PublicKey))
                .Build(new Account[] { mintAuth, mintAccount });

            var sign = await rpc.SendTransactionAsync(tx);

            //calculate the ATA
            var tokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount
                                    (mintAuth.PublicKey, mintAccount.PublicKey);
            //create token account for store token
            var tx2 = new TransactionBuilder()
                .SetFeePayer(mintAuth)
                .SetRecentBlockHash(blockhash)
                .AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    mintAuth.PublicKey,
                    mintAuth.PublicKey,
                    mintAccount.PublicKey
                    )).Build(mintAuth);

            var sign2 = await rpc.SendTransactionAsync(tx2);

            var tx3 = new TransactionBuilder()
                .SetFeePayer(mintAuth)
                .SetRecentBlockHash (blockhash)
                .AddInstruction(
                        TokenProgram.MintTo(
                            mintAccount.PublicKey,
                            tokenAccount,
                            1000000000,
                            mintAuth.PublicKey))
                .Build(mintAuth);

            var sign3 = await rpc.SendTransactionAsync(tx3);

            return Ok(new
            {
                sign,
                sign2,
                sign3
            });

        }

        public class TransferRequest
        {
            public string sender { get; set; } = string.Empty;
            public string Recipient { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }
    }
}
