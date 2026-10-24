namespace ResourceManagementSystem.Database.Models;

public class IdentityVerificationEntity
{
    public int Id { get; set; }
    public int StaffId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string MaskedDocumentNumber { get; set; } = string.Empty;
    public VerificationResult Result { get; set; } = VerificationResult.Pending;
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
