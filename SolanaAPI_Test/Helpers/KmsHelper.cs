using Amazon;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SolanaAPI_Test.Helpers
{
    public class KmsHelper
    {
        private readonly string _kmsKeyId;
        //private readonly RegionEndpoint _region = RegionEndpoint.APSoutheast1;
        private readonly IAmazonKeyManagementService _kmsClient;

        public KmsHelper()
        {
            var configuration = new ConfigurationBuilder()
                  .SetBasePath(Directory.GetCurrentDirectory())
                  .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                  .Build();

            _kmsKeyId = configuration["AWS:KMS"]
                        ?? throw new ArgumentNullException("AWS:KMS setting is missing.");

            _kmsClient = new AmazonKeyManagementServiceClient(RegionEndpoint.APSoutheast2);
        }

        public async Task<byte[]> KmsWrapKey(byte[] dek)
        {
            var request = new EncryptRequest
            {
                KeyId = _kmsKeyId,
                Plaintext = new MemoryStream(dek)
            };
            
            var response = await _kmsClient.EncryptAsync(request);
            //var response = await _kmsClient.CreateKe;
            
            return response.CiphertextBlob.ToArray();
        }

        public async Task<byte[]> KmsUnwrapKey(byte[] encryptDek)
        {
            var request = new DecryptRequest
            {
                CiphertextBlob = new MemoryStream(encryptDek)
            };

            var response = await _kmsClient.DecryptAsync(request);
            return response.Plaintext.ToArray();
        }
    }
}
