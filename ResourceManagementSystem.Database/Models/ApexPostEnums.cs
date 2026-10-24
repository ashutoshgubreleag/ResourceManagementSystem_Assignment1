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
    BankTransfer,
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
    Parcel,
    Letter
}

public enum ShippingServiceLevel
{
    Standard,
    Express,
    Registered
}

public enum DestinationZone
{
    Local,
    Regional,
    National,
    International
}

public enum MoneyOrderStatus
{
    Drafted,
    Approved,
    Rejected,
    Paid
}

public enum VerificationResult
{
    Pass,
    Fail,
    Pending
}

public enum DocumentType
{
    Passport,
    DriverLicense,
    NationalId,
    ResidencePermit
}

public enum TransactionLineType
{
    RetailItem,
    Service
}
