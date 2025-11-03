using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Org.BouncyCastle.Utilities.Encoders;
using SolanaAPI_Test.Helpers;
using SolanaAPI_Test.Interface;
using SolanaAPI_Test.Models;
using SolanaAPI_Test.Services;
using Solnet.KeyStore;
using Solnet.KeyStore.Services;
using Solnet.Programs;
using Solnet.Programs.Models.TokenProgram;
using Solnet.Programs.TokenSwap;
using Solnet.Rpc;
using Solnet.Rpc.Builders;
using Solnet.Rpc.Models;
using Solnet.Wallet;
using Solnet.Wallet.Bip39;
using Solnet.Wallet.Utilities;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Solnet.Programs.Models.Stake.State;

namespace SolanaAPI_Test.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SolWalletController(IWalletRepo walletrepo, SolanaNftService nftService) : ControllerBase
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
                //Id = Guid.NewGuid(),
                //UserId = Guid.NewGuid(),
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
                //string enDek; 
                //string enPk; 
                //var walletDetail = await walletrepo.GetUserWallet(request.sender); 
                //enDek = walletDetail.EncryptedDek; 
                //enPk = walletDetail.EncryptedPrivateKey; 

                ///* Decrypt step: 
                // * 1.Get Encypted Data from db 
                // * 2.Decrypt Dek using aws kms 
                // * 3.Decypte Private Key 
                // */ 

                //byte[] encryptedDekBytes = Convert.FromBase64String(enDek);
                //var kmsHelper = new KmsHelper(); 
                //byte[] deDek = await kmsHelper.KmsUnwrapKey(encryptedDekBytes); 

                //byte[] decryptedPrivateKey = AesGcmHelpers.DecryptFromBase64(deDek, enPk);

                var decryptedAccount = await DecryptAccount(request.sender);

                var encoder = new Base58Encoder();
                //string privateKeyBase58 = encoder.EncodeData(decryptedPrivateKey.PrivateKey);//string format private key
                byte[] bytePublicKey = encoder.DecodeData(request.sender);

                var sender = new Account(decryptedAccount.PrivateKey, bytePublicKey);

                var rpc = GetRpcClient("devnet");

                var balanceResponse = await rpc.GetBalanceAsync(sender.PublicKey);
                decimal balance = balanceResponse.Result.Value / LamportsPerSol;

                if (balance < request.Amount)
                {
                    return BadRequest("Insufficient balance");
                }
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

            var encoder = new Base58Encoder();
            byte[] bytePublicKey = encoder.DecodeData(minAuthPublicKey);

            Account mintAuth = new Account(decryptedPrivateKey, bytePublicKey);

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

        [HttpPost("mintToken")]
        public async Task<IActionResult> MintToken(string mintAuthAcc, string mintAccountAdd, string tokenAccAdd)
        {
            var decMintAccount = await DecryptAccount(mintAuthAcc);

            var encoder = new Base58Encoder();
            byte[] bytePublicKey = encoder.DecodeData(mintAuthAcc);

            Account mintAuth = new Account(decMintAccount.PrivateKey, bytePublicKey);

            var mintAccount = new PublicKey(mintAccountAdd);
            var tokenAccount = new PublicKey(tokenAccAdd);

            var rpc = GetRpcClient("devnet");
            var blockhash = (await rpc.GetLatestBlockHashAsync()).Result.Value.Blockhash;


            var tx3 = new TransactionBuilder()
                .SetFeePayer(mintAuth)
                .SetRecentBlockHash(blockhash)
                .AddInstruction(
                        TokenProgram.MintTo(
                            mintAccount,
                            tokenAccount,
                            1000000000,
                            mintAuth.PublicKey))
                .Build(mintAuth);

            var sign3 = await rpc.SendTransactionAsync(tx3);

            if (sign3.WasSuccessful)
            {
                return Ok(new
                {
                    sign3
                });
            }
            return BadRequest("Invalid");
        }

        [HttpPost("searchTransaction")]
        public async Task<IActionResult> SearchRecentTransaction(string sign)
        {
            //var publicKey = new PublicKey(addressToSearch);

            var rpc = GetRpcClient("devnet");

            var result = await rpc.GetTransactionAsync(sign);

            if (result.WasSuccessful) { return Ok(result); }

            return BadRequest("InvalidAddress");
        }

        [HttpPost("addressSignHistory")]
        public async Task<IActionResult> GetAddressSignedHisroty(string address)
        {
            var rpc = GetRpcClient("");
            var signatures = await rpc.GetSignaturesForAddressAsync(address,limit:10);

            if (signatures.WasSuccessful) 
            { 
                return Ok(signatures.Result);
            }
            return BadRequest("InvalidAddress");
        }

        //[HttpPost("searchTransactionStatus")]
        //public async Task<IActionResult> SearhcTransactionStatus(string signature)
        //{
        //    var rpc = GetRpcClient("devnet");

        //    var result = await rpc.GetSignatureStatusesAsync(signature);
        //}

        [HttpPost("encryptfunc")]
        public async Task<IActionResult> EncryptFunc(string ToEncrypt)
        {
            byte[] dek = RandomNumberGenerator.GetBytes(32);
            byte[] privateKeyBytes = Encoding.UTF8.GetBytes(ToEncrypt);
            var encryptedPrivateKey = AesGcmHelpers.EncryptToBase64(dek, privateKeyBytes); //To store

            byte[] decryptedPrivateKey = AesGcmHelpers.DecryptFromBase64(dek, encryptedPrivateKey);
            string privateKeyString = Encoding.UTF8.GetString(decryptedPrivateKey);


            return Ok(new
            {
                ByteBeforEn = privateKeyBytes,
                StringAfterEn = encryptedPrivateKey,
                ByteAfterDec = decryptedPrivateKey,
                StringAfterDe = privateKeyString,

            });
        }
        
        [HttpGet("getTokenAccBal")]
        public async Task<IActionResult> GetTokenBalance(string accAddress)
        {
            var tokenAcc = new PublicKey(accAddress);
            var rpc = GetRpcClient("devnet");

            var tokenAccountInfo = await rpc.GetTokenAccountBalanceAsync(tokenAcc);

            if (tokenAccountInfo.WasSuccessful)
            {
                return Ok(new
                {
                    tokanbalance = tokenAccountInfo.Result.Value.Amount,
                    decimals = tokenAccountInfo.Result.Value.Decimals,
                    UiAmount = tokenAccountInfo.Result.Value.UiAmountString
                });
            }
            return BadRequest("Invalid Token Account");

        }

        [HttpGet("getTokenAccBalByOwner")]
        public async Task<IActionResult> GetTokenBalanceByOwner(string walletAddress)
        {
            var owenerPublicKey = new PublicKey(walletAddress);
            var rpc = GetRpcClient("devnet");

            var tokenAccount = await rpc.GetTokenAccountsByOwnerAsync(owenerPublicKey,
                "GKBcXFv98SGEsaRek9w5fEJtq3B3bMbky194PYXS2gVe");

            if (tokenAccount.WasSuccessful)
            {
                return Ok(new { tokenAccount.Result });
            }
            return BadRequest("Invalid");
        }

        [HttpGet("calTokenAcc")]
        public async Task<IActionResult> GetTokenAcc (string owner, string mint)
        {
            var ownerPublic = new PublicKey(owner);
            var mintPublic = new PublicKey(mint);
            var tokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount
                                    (ownerPublic, mintPublic);

            return Ok(new
            {
                tokenAccount
            });
        }

        /// <summary>
        /// 获取指定 NFT 的详细信息
        /// </summary>
        /// <param name="mintAddress">NFT 的 Mint Address</param>
        /// <returns>NFT 详细信息</returns>
        [HttpGet("{mintAddress}")]
        [ProducesResponseType(typeof(NftInfo), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<ActionResult<NftInfo>> GetNftInfo(string mintAddress)
        {
            try
            {
                var nftInfo = await nftService.GetNftInfoAsync(mintAddress);
                return Ok(nftInfo);
            }
            catch (Exception ex) when (ex.Message.Contains("not found"))
            {
                return NotFound(new { message = "NFT not found", mintAddress });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve NFT info", error = ex.Message });
            }
        }
        //public string GetMetadataPDA(string mintAddress)
        //{
        //    var mintPublicKey = new PublicKey(mintAddress);
        //    var metadataProgramPublicKey = new PublicKey(MetaplexTokenMetadataProgramId)
        //}


        //[HttpPost("createStackAccount")]
        //public async Task<IActionResult> CreateStakeAccount(string authAccount)
        //{
        //    var enAccPrivatekey = await DecryptAccount(authAccount);
        //    var encoder = new Base58Encoder();
        //    byte[] bytePublicKey = encoder.DecodeData(authAccount);
        //    Account authAcc = new Account(enAccPrivatekey.PrivateKey, bytePublicKey);

        //    var authorized = new Authorized
        //    {
        //        Staker = authAcc.PublicKey,
        //        Withdrawer = authAcc.PublicKey
        //    };

        //    var stakeAccount = new Account();

        //    var rpc = GetRpcClient("");
        //    var rentExemption = (await rpc.GetMinimumBalanceForRentExemptionAsync(StakeProgram.StakeAccountDataSize)).Result;

        //    ulong stakeAmount = 1_000_000_000UL;
        //    var createAccountIx = SystemProgram.CreateAccount(
        //        fromAccount: authAcc.PublicKey,
        //        newAccountPublicKey: stakeAccount.PublicKey,
        //        lamports: stakeAmount + rentExemption
        //        );

        //    var lockup = new Lockup();

        //    var initializeIx = StakeProgram.Initialize(
        //                        stakeAccount.PublicKey,
        //                        authorized,
        //                        lockup
        //                    );

        //    return Ok();
        //}

        [HttpPost("createTokenAccount")]
        public async Task<IActionResult> CreateTokenAccount(string PublicAccont)
        {
            var decAcc = await DecryptAccount(PublicAccont);
            PublicKey mintAcc = new("GKBcXFv98SGEsaRek9w5fEJtq3B3bMbky194PYXS2gVe");
            Account Account = new(decAcc.PrivateKey, await ConvertToByte(PublicAccont));

            var tx2 = new TransactionBuilder()
                .SetRecentBlockHash(await GetBlockHash())
                .SetFeePayer(Account)
                .AddInstruction(AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                    Account,
                    Account,
                    mintAcc
                    )).Build(Account);
            var rpc = GetRpcClient("devnet");
            var sign = await rpc.SendTransactionAsync(tx2);

            if (sign.WasSuccessful)
            {
                return Ok(new
                {
                    sign
                });
            }
            return BadRequest("error");
        }

        [HttpPost("transferToken")]
        public async Task<IActionResult> TransferToken(TransferTokenRequest request)
        {


            PublicKey sendeAccountPublic = new(request.senderAccountPublic);
            PublicKey senderATAPublic = new(request.senderTokenAccountPublic);
            PublicKey receiverAccountPublic = new(request.receiverAccountPublic);
            PublicKey mintAccPublic = new(request.mintAccount);

            if (string.IsNullOrEmpty(request.receiverTokenAccountPublic))
            {//auto create token account
                request.receiverTokenAccountPublic = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(receiverAccountPublic, mintAccPublic);
            }
            PublicKey receiverATAPublic = new(request.receiverTokenAccountPublic);

            var instruction = TokenProgram.Transfer(
                source: senderATAPublic,
                destination: receiverATAPublic,
                amount: request.amount,
                authority: sendeAccountPublic);

            var rpc = GetRpcClient("devnet");
            var blockHash = (await rpc.GetLatestBlockHashAsync()).Result.Value.Blockhash;

            var decAcc = await DecryptAccount(sendeAccountPublic);

            var encoder = new Base58Encoder();
            byte[] bytePublicKey = encoder.DecodeData(request.senderAccountPublic);
            Account senderAcc = new(decAcc.PrivateKey, bytePublicKey);

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(sendeAccountPublic)
                .AddInstruction(instruction)
                .Build(senderAcc);

            var result = await rpc.SendTransactionAsync(tx);
            if (result.WasSuccessful)
            {
                return Ok(new
                {
                    success = true,
                    signature = result
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Reason
                });
            }

        }

        [HttpPost("CallProgram")]
        public async Task<IActionResult> CallSPL(string publicKey, string message)
        {
            var rpc = GetRpcClient("devnet");

            PublicKey programId = new("CdZVrRGQWpRBeEnafaMwKArGdPEghdiy8RBBmehvTsjt");

            byte[] instructionData = Encoding.UTF8.GetBytes(message);

            var accPub = await ConvertToByte(publicKey);

            var decAcc = await DecryptAccount(publicKey);

            Account senderAcc = new(decAcc.PrivateKey, accPub);

            var blockHash = await GetBlockHash();

            var tx = new TransactionBuilder()
                .SetRecentBlockHash(blockHash)
                .SetFeePayer(senderAcc)
                .AddInstruction(new TransactionInstruction
                {
                    ProgramId = programId,
                    Keys = new List<AccountMeta>
                    {
                        AccountMeta.Writable(senderAcc.PublicKey, true)
                    },
                    Data = instructionData
                })
                .Build(senderAcc);

            var result = await rpc.SendTransactionAsync(tx);
            return Ok(result);
        }

        private async Task<AccountDetial> DecryptAccount(string accToDec)
        {
            ///* Decrypt step: 
            // * 1.Get Encypted Data from db 
            // * 2.Decrypt Dek using aws kms 
            // * 3.Decypte Private Key 
            // */ 

            byte[] deDek = [];
            byte[] decryptedPrivateKey = [];

            string enDek;
            string enPrivateKey;

            var walletDetail = await walletrepo.GetUserWallet(accToDec);

            enDek = walletDetail.EncryptedDek;
            enPrivateKey = walletDetail.EncryptedPrivateKey;

            byte[] dek = Convert.FromBase64String(enDek);
            var kmsHelper = new KmsHelper();
            deDek = await kmsHelper.KmsUnwrapKey(dek);

            decryptedPrivateKey = AesGcmHelpers.DecryptFromBase64(deDek, enPrivateKey);

            var result = new AccountDetial
            {
                PrivateKey = decryptedPrivateKey
            };

            return result;
        }

        [HttpGet("checkProgram")]
        public async Task<IActionResult> GetProgramDetail(string programId)
        {
            var rpc = GetRpcClient("devnet");
            var result = await rpc.GetProgramAccountsAsync(programId);


            return Ok(result);
        }


        private async Task<Byte[]>ConvertToByte(string address)
        {
            var encoder = new Base58Encoder();
            byte[] bytePublicKey = encoder.DecodeData(address);
            return bytePublicKey;
        }

        private async Task<string> GetBlockHash()
        {
            var rpc = GetRpcClient("devnet");
            var blockHash = (await rpc.GetLatestBlockHashAsync()).Result.Value.Blockhash;
            return blockHash;
        }

        public class AccountDetial
        {
            public byte[] PrivateKey = [];
        }
        public class TransferRequest
        {
            public string sender { get; set; } = string.Empty;
            public string Recipient { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }

        public class TransferTokenRequest
        {
            public string senderAccountPublic { get; set; }
            public string senderTokenAccountPublic { get; set; }
            public string receiverAccountPublic { get; set; }
            public string receiverTokenAccountPublic { get; set; }
            public string mintAccount {  get; set; }
            
            public ulong amount { get; set; }
        }
    }
}
