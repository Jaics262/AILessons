public sealed class CalculateTotalTool
{
    public decimal CalculateTotal(int quantity, decimal unitPrice)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        return quantity * unitPrice;
    }
}
