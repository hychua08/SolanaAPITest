using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SolanaAPI_Test.Models
{
    public class UserWallet
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        [MaxLength(255)]
        public string PublicKey { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string KeystoreJson { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string EncryptedDek { get; set; }
        [Column(TypeName = "nvarchar(max)")]
        public string EncryptedPrivateKey { get; set; }

        [MaxLength(100)]
        public string CryptoAlgorithm { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "nvarchar(max)")]
        public string Metadata { get; set; }
    }
}
