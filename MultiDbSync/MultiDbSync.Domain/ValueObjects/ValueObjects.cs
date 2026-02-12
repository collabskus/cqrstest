namespace MultiDbSync.Domain.ValueObjects;

public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero => new(0, "USD");

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot add money with different currencies");
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException("Cannot subtract money with different currencies");
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(int multiplier, Money money)
    {
        return money * multiplier;
    }

    public override string ToString() => $"{Amount:C} {Currency}";
}

public sealed record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public string FullAddress => $"{Street}, {City}, {State} {PostalCode}, {Country}";
}

public sealed record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty", nameof(value));
        if (!value.Contains('@'))
            throw new ArgumentException("Invalid email format", nameof(value));
        Value = value.ToLowerInvariant();
    }

    public static implicit operator string(EmailAddress email) => email.Value;
    public override string ToString() => Value;
}
