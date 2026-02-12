using MultiDbSync.Domain.Entities;

namespace MultiDbSync.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IProductRepository : IRepository<Product>
{
    Task<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(int threshold, CancellationToken cancellationToken = default);
    Task<Product?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetRecentOrdersAsync(int count, CancellationToken cancellationToken = default);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Customer>> SearchByNameAsync(string namePattern, CancellationToken cancellationToken = default);
}

public interface ISyncOperationRepository
{
    Task<SyncOperation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncOperation>> GetOperationsByNodeIdAsync(string nodeId, CancellationToken cancellationToken = default);
    Task AddAsync(SyncOperation operation, CancellationToken cancellationToken = default);
    Task UpdateAsync(SyncOperation operation, CancellationToken cancellationToken = default);
}

public interface IDatabaseNodeRepository
{
    Task<DatabaseNode?> GetByIdAsync(string nodeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseNode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseNode>> GetHealthyNodesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DatabaseNode>> GetPrimaryNodesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(DatabaseNode node, CancellationToken cancellationToken = default);
    Task UpdateAsync(DatabaseNode node, CancellationToken cancellationToken = default);
    Task DeleteAsync(string nodeId, CancellationToken cancellationToken = default);
}
