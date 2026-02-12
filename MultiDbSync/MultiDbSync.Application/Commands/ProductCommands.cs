using MultiDbSync.Application.CQRS;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Domain.ValueObjects;

namespace MultiDbSync.Application.Commands;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    string Category) : Command;

public sealed record UpdateProductPriceCommand(
    Guid ProductId,
    decimal NewPrice,
    string Currency) : Command;

public sealed record UpdateProductStockCommand(
    Guid ProductId,
    int NewQuantity) : Command;

public sealed record AdjustProductStockCommand(
    Guid ProductId,
    int Adjustment) : Command;

public sealed record DeleteProductCommand(Guid ProductId) : Command;

public sealed class CreateProductCommandHandler(
    IProductRepository productRepository,
    ISynchronizationService synchronizationService)
    : ICommandHandler<CreateProductCommand, Product>
{
    public async Task<CommandResult<Product>> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = new Product(
                command.Name,
                command.Description,
                new Money(command.Price, command.Currency),
                command.StockQuantity,
                command.Category);

            await productRepository.AddAsync(product, cancellationToken);

            await synchronizationService.SyncEntityAsync(
                product,
                OperationType.Create,
                cancellationToken);

            return CommandResult<Product>.Success(product);
        }
        catch (Exception ex)
        {
            return CommandResult<Product>.Failure($"Failed to create product: {ex.Message}");
        }
    }
}

public sealed class UpdateProductPriceCommandHandler(
    IProductRepository productRepository,
    ISynchronizationService synchronizationService)
    : ICommandHandler<UpdateProductPriceCommand, Product>
{
    public async Task<CommandResult<Product>> HandleAsync(
        UpdateProductPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productRepository.GetByIdAsync(command.ProductId, cancellationToken);
            if (product is null)
                return CommandResult<Product>.Failure("Product not found");

            product.UpdatePrice(new Money(command.NewPrice, command.Currency));

            await productRepository.UpdateAsync(product, cancellationToken);

            await synchronizationService.SyncEntityAsync(
                product,
                OperationType.Update,
                cancellationToken);

            return CommandResult<Product>.Success(product);
        }
        catch (Exception ex)
        {
            return CommandResult<Product>.Failure($"Failed to update product price: {ex.Message}");
        }
    }
}

public sealed class UpdateProductStockCommandHandler(
    IProductRepository productRepository,
    ISynchronizationService synchronizationService)
    : ICommandHandler<UpdateProductStockCommand, Product>
{
    public async Task<CommandResult<Product>> HandleAsync(
        UpdateProductStockCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productRepository.GetByIdAsync(command.ProductId, cancellationToken);
            if (product is null)
                return CommandResult<Product>.Failure("Product not found");

            product.UpdateStock(command.NewQuantity);

            await productRepository.UpdateAsync(product, cancellationToken);

            await synchronizationService.SyncEntityAsync(
                product,
                OperationType.Update,
                cancellationToken);

            return CommandResult<Product>.Success(product);
        }
        catch (Exception ex)
        {
            return CommandResult<Product>.Failure($"Failed to update product stock: {ex.Message}");
        }
    }
}

public sealed class AdjustProductStockCommandHandler(
    IProductRepository productRepository,
    ISynchronizationService synchronizationService)
    : ICommandHandler<AdjustProductStockCommand, Product>
{
    public async Task<CommandResult<Product>> HandleAsync(
        AdjustProductStockCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productRepository.GetByIdAsync(command.ProductId, cancellationToken);
            if (product is null)
                return CommandResult<Product>.Failure("Product not found");

            product.AdjustStock(command.Adjustment);

            await productRepository.UpdateAsync(product, cancellationToken);

            await synchronizationService.SyncEntityAsync(
                product,
                OperationType.Update,
                cancellationToken);

            return CommandResult<Product>.Success(product);
        }
        catch (Exception ex)
        {
            return CommandResult<Product>.Failure($"Failed to adjust product stock: {ex.Message}");
        }
    }
}

public sealed class DeleteProductCommandHandler(
    IProductRepository productRepository,
    ISynchronizationService synchronizationService)
    : ICommandHandler<DeleteProductCommand, bool>
{
    public async Task<CommandResult<bool>> HandleAsync(
        DeleteProductCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productRepository.GetByIdAsync(command.ProductId, cancellationToken);
            if (product is null)
                return CommandResult<bool>.Failure("Product not found");

            await productRepository.DeleteAsync(command.ProductId, cancellationToken);

            await synchronizationService.SyncEntityAsync(
                product,
                OperationType.Delete,
                cancellationToken);

            return CommandResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return CommandResult<bool>.Failure($"Failed to delete product: {ex.Message}");
        }
    }
}
