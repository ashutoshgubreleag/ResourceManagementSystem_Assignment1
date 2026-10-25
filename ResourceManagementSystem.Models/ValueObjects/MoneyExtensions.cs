namespace ResourceManagementSystem.Models.ValueObjects;

/// <summary>
/// C# 14 extension members. Rounding and summing are needed everywhere money is calculated
/// (line totals, tax, report totals). Before C# 14 these had to be extension *methods*
/// (<c>RoundToCents(m)</c>); extension *properties* read like part of the type
/// (<c>tax.RoundedToCents</c>) without bloating the Money record itself.
/// </summary>
public static class MoneyExtensions
{
    extension(Money money)
    {
        /// <summary>Banker-safe rounding for cash: half away from zero to whole cents.</summary>
        public Money RoundedToCents => new(Math.Round(money.Amount, 2, MidpointRounding.AwayFromZero));

        public bool IsZero => money.Amount == 0m;

        public bool IsNegative => money.Amount < 0m;

        /// <summary>Applies a rate (e.g. 0.10 for 10 % tax) and rounds to cents.</summary>
        public Money ApplyRate(decimal rate) => new Money(money.Amount * rate).RoundedToCents;
    }

    extension(IEnumerable<Money> amounts)
    {
        /// <summary>Total of a sequence of amounts (LINQ's Sum does not know about Money).</summary>
        public Money Total()
        {
            var total = 0m;
            foreach (var amount in amounts)
            {
                total += amount.Amount;
            }

            return new Money(total);
        }
    }

    extension(decimal amount)
    {
        /// <summary>Lets callers write <c>12.50m.AsMoney</c>.</summary>
        public Money AsMoney => new(amount);
    }
}
