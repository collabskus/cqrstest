using MultiDbSync.Domain.ValueObjects;

namespace MultiDbSync.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Money Price { get; private set; } = Money.Zero;
    public int StockQuantity { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    private Product() { }

    public Product(
        string name,
        string description,
        Money price,
        int stockQuantity,
        string category)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        Price = price ?? throw new ArgumentNullException(nameof(price));
        StockQuantity = stockQuantity;
        Category = category ?? "General";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();
    }

    public void UpdatePrice(Money newPrice)
    {
        Price = newPrice ?? throw new ArgumentNullException(nameof(newPrice));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateStock(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative", nameof(quantity));
        StockQuantity = quantity;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void AdjustStock(int adjustment)
    {
        var newStock = StockQuantity + adjustment;
        if (newStock < 0)
            throw new InvalidOperationException("Insufficient stock");
        StockQuantity = newStock;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateDetails(string name, string description, string category)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? string.Empty;
        Category = category ?? "General";
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private void IncrementVersion()
    {
        Version = Guid.NewGuid().ToByteArray();
    }
}

public sealed class Order
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; } = Money.Zero;
    public Address ShippingAddress { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public Order(
        Guid customerId,
        Address shippingAddress)
    {
        Id = Guid.NewGuid();
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        ShippingAddress = shippingAddress ?? throw new ArgumentNullException(nameof(shippingAddress));
        TotalAmount = Money.Zero;
        CreatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();
    }

    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        var existingItem = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
        }
        else
        {
            _items.Add(new OrderItem(Id, product.Id, product.Price, quantity));
        }

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void RemoveItem(Guid productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            _items.Remove(item);
            RecalculateTotal();
            UpdatedAt = DateTime.UtcNow;
            IncrementVersion();
        }
    }

    public void MarkAsShipped()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be shipped");
        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOperationException("Only shipped orders can be delivered");
        Status = OrderStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void Cancel()
    {
        if (Status == OrderStatus.Delivered)
            throw new InvalidOperationException("Cannot cancel delivered orders");
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Aggregate(
            Money.Zero,
            (total, item) => total + item.Subtotal);
    }

    private void IncrementVersion()
    {
        Version = Guid.NewGuid().ToByteArray();
    }

    private DateTime UpdatedAt { get; set; }
}

public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Money UnitPrice { get; private set; } = Money.Zero;
    public int Quantity { get; private set; }
    public Money Subtotal => UnitPrice * Quantity;

    private OrderItem() { }

    public OrderItem(Guid orderId, Guid productId, Money unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
        Quantity = quantity;
    }

    public void IncreaseQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        Quantity += amount;
    }

    public void DecreaseQuantity(int amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        if (amount > Quantity)
            throw new ArgumentException("Cannot decrease more than available quantity", nameof(amount));
        Quantity -= amount;
    }
}

public enum OrderStatus
{
    Pending,
    Shipped,
    Delivered,
    Cancelled
}

public sealed class Customer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public EmailAddress Email { get; private set; } = null!;
    public Address? ShippingAddress { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    private Customer() { }

    public Customer(string name, EmailAddress email)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();
    }

    public void UpdateShippingAddress(Address address)
    {
        ShippingAddress = address ?? throw new ArgumentNullException(nameof(address));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    public void UpdateName(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        UpdatedAt = DateTime.UtcNow;
        IncrementVersion();
    }

    private void IncrementVersion()
    {
        Version = Guid.NewGuid().ToByteArray();
    }
}
