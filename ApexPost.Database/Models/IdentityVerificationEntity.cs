using ApexPost.Models.ValueObjects;

namespace ApexPost.Database.Models;

/// <summary>
/// Result of checking a customer's identity document. The full document number is never
/// stored: <see cref="MaskedDocumentNumber"/> can only hold the last four characters.
/// </summary>
public class IdentityVerificationEntity : IEntity
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public BranchCode BranchCode { get; set; }
    public int StaffId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public MaskedDocumentNumber MaskedDocumentNumber { get; set; }
    public VerificationResult Result { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}
