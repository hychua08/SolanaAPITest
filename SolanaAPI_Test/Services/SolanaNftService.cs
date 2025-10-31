using SolanaAPI_Test.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolanaAPI_Test.Services
{
    public class SolanaNftService
    {
        private readonly HttpClient _httpClient;
        private readonly string _rpcUrl;

        public SolanaNftService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _rpcUrl = config["Solana:RpcUrl"];
        }

        public async Task<NftInfo> GetNftInfoAsync(string mintAddress)
        {

            var assestRequest = new
            {
                jsonrpc = "2.0",
                id = "nft-query",
                method = "getAsset",
                @params = new { id = mintAddress }
            };

            var response = await _httpClient.PostAsJsonAsync(_rpcUrl, assestRequest);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch NFT info: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var dasResponse = JsonSerializer.Deserialize<DasAssetResponse>(
                jsonResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dasResponse?.Result == null)
            {
                throw new Exception("NFT not found");
            }

            var metadata = await GetOffChainMetadataAsync(dasResponse.Result.Content.JsonUri);

            return new NftInfo
            {
                MintAddress = mintAddress,
                Name = dasResponse.Result.Content.Metadata.Name,
                Symbol = dasResponse.Result.Content.Metadata.Symbol,
                Description = metadata?.Description ?? "",
                ImageUrl = metadata?.Image ?? dasResponse.Result.Content.Links?.Image ?? "",
                MetadataUri = dasResponse.Result.Content.JsonUri,
                Attributes = metadata?.Attributes?.Select(a => new NftAttribute
                {
                    TraitType = a.TraitType,
                    Value = a.Value
                }).ToList() ?? new List<NftAttribute>(),
                Collection = dasResponse.Result.Grouping?.FirstOrDefault()?.GroupValue != null
                    ? new NftCollection
                    {
                        Name = dasResponse.Result.Content.Metadata.Name,
                        Family = dasResponse.Result.Grouping.FirstOrDefault()?.GroupValue
                    }
                    : null,
                Creators = dasResponse.Result.Creators?.Select(c => new NftCreator
                {
                    Address = c.Address,
                    Share = c.Share
                }).ToArray() ?? Array.Empty<NftCreator>()
            };
        }

        private async Task<OffChainMetadata?> GetOffChainMetadataAsync(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return null;
            }

            try
            {
                if (uri.StartsWith("ipfs://"))
                {
                    uri = uri.Replace("ipfs://", "https://ipfs.io/ipfs/");
                }

                var response = await _httpClient.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OffChainMetadata>(json);
            }
            catch
            {
                return null;
            }
            
        }

        // DAS API Response Models
        public class DasAssetResponse
        {
            [JsonPropertyName("result")]
            public DasAsset Result { get; set; }
        }

        public class DasAsset
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("content")]
            public DasContent Content { get; set; }

            [JsonPropertyName("grouping")]
            public List<DasGrouping> Grouping { get; set; }

            [JsonPropertyName("creators")]
            public List<DasCreator> Creators { get; set; }
        }

        public class DasContent
        {
            [JsonPropertyName("json_uri")]
            public string JsonUri { get; set; }

            [JsonPropertyName("metadata")]
            public DasMetadata Metadata { get; set; }

            [JsonPropertyName("links")]
            public DasLinks Links { get; set; }
        }

        public class DasMetadata
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("symbol")]
            public string Symbol { get; set; }
        }

        public class DasLinks
        {
            [JsonPropertyName("image")]
            public string Image { get; set; }
        }

        public class DasGrouping
        {
            [JsonPropertyName("group_value")]
            public string GroupValue { get; set; }
        }

        public class DasCreator
        {
            [JsonPropertyName("address")]
            public string Address { get; set; }

            [JsonPropertyName("share")]
            public int Share { get; set; }
        }

        // Off-chain Metadata Model
        public class OffChainMetadata
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("image")]
            public string Image { get; set; }

            [JsonPropertyName("attributes")]
            public List<MetadataAttribute> Attributes { get; set; }
        }

        public class MetadataAttribute
        {
            [JsonPropertyName("trait_type")]
            public string TraitType { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }
    }
}
