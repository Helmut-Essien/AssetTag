namespace AssetTag.Models
{
    public class RefreshTokens
    {
        public int Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime Expires { get; set; }
        public DateTime Created { get; set; }
        public string CreatedByIp { get; set; } = string.Empty;
        public DateTime? Revoked { get; set; }
        public string? RevokedByIp { get; set; }
        public string? ReplacedByToken { get; set; }
        public bool isExpired => DateTime.UtcNow >= Expires; 
        public bool isActive => Revoked == null && !isExpired;

        // Navigation property
        //FK to ApplicationUser
        public string ApplicationUserId { get; set; } = string.Empty;
        public ApplicationUser? ApplicationUser { get; set; } = null;
    }
}
