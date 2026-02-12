using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.ValueObjects;
using Xunit;

namespace MultiDbSync.Tests.Domain;

public class ProductTests
{
    [Fact]
    public void CreateProduct_WithValidData_ShouldCreateSuccessfully()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Test Product", product.Name);
        Assert.Equal("Test Description", product.Description);
        Assert.Equal(99.99m, product.Price.Amount);
        Assert.Equal(100, product.StockQuantity);
        Assert.Equal("Test Category", product.Category);
    }

    [Fact]
    public void UpdatePrice_WithValidPrice_ShouldUpdateSuccessfully()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        product.UpdatePrice(new Money(79.99m, "USD"));

        Assert.Equal(79.99m, product.Price.Amount);
        Assert.True(product.UpdatedAt > product.CreatedAt);
    }

    [Fact]
    public void UpdateStock_WithValidQuantity_ShouldUpdateSuccessfully()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        product.UpdateStock(50);

        Assert.Equal(50, product.StockQuantity);
    }

    [Fact]
    public void UpdateStock_WithNegativeQuantity_ShouldThrowException()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        Assert.Throws<ArgumentException>(() => product.UpdateStock(-10));
    }

    [Fact]
    public void AdjustStock_WithPositiveAdjustment_ShouldIncreaseStock()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        product.AdjustStock(50);

        Assert.Equal(150, product.StockQuantity);
    }

    [Fact]
    public void AdjustStock_WithNegativeAdjustment_ShouldDecreaseStock()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        product.AdjustStock(-50);

        Assert.Equal(50, product.StockQuantity);
    }

    [Fact]
    public void AdjustStock_WithExcessiveNegative_ShouldThrowException()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        Assert.Throws<InvalidOperationException>(() => product.AdjustStock(-150));
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateSuccessfully()
    {
        var product = new Product(
            "Test Product",
            "Test Description",
            new Money(99.99m, "USD"),
            100,
            "Test Category");

        product.UpdateDetails("New Name", "New Description", "New Category");

        Assert.Equal("New Name", product.Name);
        Assert.Equal("New Description", product.Description);
        Assert.Equal("New Category", product.Category);
    }
}

public class OrderTests
{
    [Fact]
    public void CreateOrder_WithValidData_ShouldCreateSuccessfully()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0m, order.TotalAmount.Amount);
    }

    [Fact]
    public void AddItem_WithValidProduct_ShouldAddItemAndRecalculateTotal()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        var product = new Product("Test", "Desc", new Money(50m, "USD"), 10, "Cat");

        order.AddItem(product, 2);

        Assert.Single(order.Items);
        Assert.Equal(100m, order.TotalAmount.Amount);
    }

    [Fact]
    public void AddItem_WithExistingProduct_ShouldIncreaseQuantity()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        var product = new Product("Test", "Desc", new Money(50m, "USD"), 10, "Cat");

        order.AddItem(product, 2);
        order.AddItem(product, 3);

        Assert.Single(order.Items);
        Assert.Equal(5, order.Items.First().Quantity);
    }

    [Fact]
    public void MarkAsShipped_WhenPending_ShouldChangeStatus()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);

        order.MarkAsShipped();

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.NotNull(order.ShippedAt);
    }

    [Fact]
    public void MarkAsShipped_WhenNotPending_ShouldThrowException()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        order.MarkAsShipped();

        Assert.Throws<InvalidOperationException>(() => order.MarkAsShipped());
    }

    [Fact]
    public void MarkAsDelivered_WhenShipped_ShouldChangeStatus()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        order.MarkAsShipped();

        order.MarkAsDelivered();

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.NotNull(order.DeliveredAt);
    }

    [Fact]
    public void Cancel_WhenPending_ShouldChangeStatus()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WhenDelivered_ShouldThrowException()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        order.MarkAsShipped();
        order.MarkAsDelivered();

        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }

    [Fact]
    public void RemoveItem_WithExistingItem_ShouldRemoveAndRecalculate()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");
        var order = new Order(Guid.NewGuid(), address);
        var product = new Product("Test", "Desc", new Money(50m, "USD"), 10, "Cat");

        order.AddItem(product, 2);
        order.RemoveItem(product.Id);

        Assert.Empty(order.Items);
        Assert.Equal(0m, order.TotalAmount.Amount);
    }
}

public class DatabaseNodeTests
{
    [Fact]
    public void CreateNode_WithValidData_ShouldCreateSuccessfully()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, true);

        Assert.Equal("node1", node.NodeId);
        Assert.Equal("Data Source=test.db", node.ConnectionString);
        Assert.Equal(1, node.Priority);
        Assert.True(node.IsPrimary);
    }

    [Fact]
    public void MarkHealthy_ShouldUpdateStatus()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, false);

        node.MarkHealthy();

        Assert.Equal(NodeStatus.Healthy, node.Status);
    }

    [Fact]
    public void MarkUnhealthy_ShouldUpdateStatus()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, false);

        node.MarkUnhealthy();

        Assert.Equal(NodeStatus.Unhealthy, node.Status);
    }

    [Fact]
    public void PromoteToPrimary_ShouldUpdatePrimaryFlag()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, false);

        node.PromoteToPrimary();

        Assert.True(node.IsPrimary);
    }

    [Fact]
    public void DemoteFromPrimary_ShouldUpdatePrimaryFlag()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, true);

        node.DemoteFromPrimary();

        Assert.False(node.IsPrimary);
    }

    [Fact]
    public void HealthScore_ShouldCalculateCorrectly()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, false);

        node.MarkHealthy();
        node.MarkHealthy();
        node.MarkHealthy();
        node.MarkUnhealthy();

        Assert.Equal(75, node.HealthScore);
    }

    [Fact]
    public void IsAlive_WhenRecentlyHeartbeat_ShouldReturnTrue()
    {
        var node = new DatabaseNode("node1", "Data Source=test.db", 1, false);
        node.MarkHealthy();

        Assert.True(node.IsAlive);
    }
}

public class ValueObjectTests
{
    [Fact]
    public void Money_Add_ShouldSumAmounts()
    {
        var money1 = new Money(50m, "USD");
        var money2 = new Money(30m, "USD");

        var result = money1 + money2;

        Assert.Equal(80m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Money_Subtract_ShouldSubtractAmounts()
    {
        var money1 = new Money(50m, "USD");
        var money2 = new Money(30m, "USD");

        var result = money1 - money2;

        Assert.Equal(20m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Money_Add_DifferentCurrencies_ShouldThrowException()
    {
        var money1 = new Money(50m, "USD");
        var money2 = new Money(30m, "EUR");

        Assert.Throws<InvalidOperationException>(() => money1 + money2);
    }

    [Fact]
    public void EmailAddress_WithValidEmail_ShouldCreateSuccessfully()
    {
        var email = new EmailAddress("test@example.com");

        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void EmailAddress_WithInvalidEmail_ShouldThrowException()
    {
        Assert.Throws<ArgumentException>(() => new EmailAddress("invalid-email"));
    }

    [Fact]
    public void Address_FullAddress_ShouldReturnFormattedAddress()
    {
        var address = new Address("123 Main St", "City", "State", "12345", "USA");

        Assert.Equal("123 Main St, City, State 12345, USA", address.FullAddress);
    }
}
