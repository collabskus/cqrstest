using MultiDbSync.Application.CQRS;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Application.Queries;

public sealed record GetProductByIdQuery(Guid ProductId) : Query;

public sealed record GetAllProductsQuery : Query;

public sealed record GetProductsByCategoryQuery(string Category) : Query;

public sealed record GetLowStockProductsQuery(int Threshold = 10) : Query;

public sealed record SearchProductsQuery(string SearchTerm) : Query;

public sealed class GetProductByIdQueryHandler(
    IProductRepository productRepository)
    : IQueryHandler<GetProductByIdQuery, Product?>
{
    public async Task<QueryResult<Product?>> HandleAsync(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await productRepository.GetByIdAsync(query.ProductId, cancellationToken);
            return QueryResult<Product?>.Success(product);
        }
        catch (Exception ex)
        {
            return QueryResult<Product?>.Failure($"Failed to get product: {ex.Message}");
        }
    }
}

public sealed class GetAllProductsQueryHandler(
    IProductRepository productRepository)
    : IQueryHandler<GetAllProductsQuery, IReadOnlyList<Product>>
{
    public async Task<QueryResult<IReadOnlyList<Product>>> HandleAsync(
        GetAllProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await productRepository.GetAllAsync(cancellationToken);
            return QueryResult<IReadOnlyList<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<Product>>.Failure($"Failed to get products: {ex.Message}");
        }
    }
}

public sealed class GetProductsByCategoryQueryHandler(
    IProductRepository productRepository)
    : IQueryHandler<GetProductsByCategoryQuery, IReadOnlyList<Product>>
{
    public async Task<QueryResult<IReadOnlyList<Product>>> HandleAsync(
        GetProductsByCategoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await productRepository.GetByCategoryAsync(query.Category, cancellationToken);
            return QueryResult<IReadOnlyList<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<Product>>.Failure($"Failed to get products by category: {ex.Message}");
        }
    }
}

public sealed class GetLowStockProductsQueryHandler(
    IProductRepository productRepository)
    : IQueryHandler<GetLowStockProductsQuery, IReadOnlyList<Product>>
{
    public async Task<QueryResult<IReadOnlyList<Product>>> HandleAsync(
        GetLowStockProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await productRepository.GetLowStockProductsAsync(query.Threshold, cancellationToken);
            return QueryResult<IReadOnlyList<Product>>.Success(products);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<Product>>.Failure($"Failed to get low stock products: {ex.Message}");
        }
    }
}

public sealed class SearchProductsQueryHandler(
    IProductRepository productRepository)
    : IQueryHandler<SearchProductsQuery, IReadOnlyList<Product>>
{
    public async Task<QueryResult<IReadOnlyList<Product>>> HandleAsync(
        SearchProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allProducts = await productRepository.GetAllAsync(cancellationToken);
            var filteredProducts = allProducts
                .Where(p => p.Name.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                           p.Description.Contains(query.SearchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return QueryResult<IReadOnlyList<Product>>.Success(filteredProducts);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<Product>>.Failure($"Failed to search products: {ex.Message}");
        }
    }
}
