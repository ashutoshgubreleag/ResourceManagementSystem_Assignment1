namespace ResourceManagementSystem.Database.Models;

public enum ProductCategory
{
    Packaging,
    Stationery,
    OfficeSupplies
}

public enum TaxCategory
{
    Standard,
    Reduced,
    Exempt
}

public enum StaffRole
{
    CounterClerk,
    BranchManager,
    Auditor,
    Administrator
}

public enum BranchStatus
{
    Active,
    Closed
}

public enum PaymentMethod
{
    Cash,
    Card,
    MobileWallet
}

public enum TransactionStatus
{
    Open,
    Paid,
    Voided,
    Refunded
}

public enum ShippingType
{
    Letter,
    Parcel
}

public enum ShippingServiceLevel
{
    Standard,
    Express,
    Registered
}

public enum ShipmentStatus
{
    Quoted,
    Lodged,
    Cancelled
}

public enum MoneyOrderStatus
{
    Pending,
    Issued,
    Cancelled
}

public enum VerificationResult
{
    Passed,
    Failed
}

public enum DocumentType
{
    Passport,
    DriverLicence,
    NationalId
}

/// <summary>
/// Discriminator for a basket line. A transaction is a composition of lines; each line
/// points at the record that backs it (product SKU, shipment, money order, verification).
/// </summary>
public enum TransactionLineType
{
    RetailItem,
    Shipping,
    MoneyOrder,
    IdentityVerification
}

public enum NotificationType
{
    LowStock,
    HighValueMoneyOrder,
    TransactionVoided,
    TransactionRefunded
}
