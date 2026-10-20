public sealed class CalculateTotalTool
{
    public decimal CalculateTotal(int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        return quantity * unitPrice;
    }

    public decimal CalculateVat(decimal amount, decimal rate)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be greater than zero.");
        }

        return amount * rate / 100m;
    }
}
