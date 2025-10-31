namespace SolanaAPI_Test.Models
{
    public class NftInfo
    {
        public string MintAddress { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string MetadataUri { get; set; }
        public List<NftAttribute> Attributes { get; set; }
        public NftCollection Collection { get; set; }
        public NftCreator[] Creators { get; set; }
    }

    public class NftAttribute
    {
        public string TraitType { get; set; }
        public string Value { get; set; }
    }

    public class NftCollection
    {
        public string Name { get; set; }
        public string Family { get; set; }
    }

    public class NftCreator
    {
        public string Address { get; set; }
        public int Share { get; set; }
    }
}
