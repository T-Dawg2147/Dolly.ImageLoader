using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IProductRepository
{
    Task<ProductContext?> GetByIdAsync(string productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductContext>> SearchProductsAsync(string searchText, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductContext>> GetByParentGroupIdAsync(string parentGroupId, CancellationToken cancellationToken);
}