using IdentityServer.Application.DTOs;
using IdentityServer.Shared.Common;
using IdentityServer.Shared.Models;

namespace IdentityServer.Application.Interfaces;

/// <summary>
/// Interface for Product service operations
/// </summary>
public interface IProductService
{
    Task<Result<PagedList<ProductDto>>> GetAllProductsAsync(PaginationParameters parameters, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> CreateProductAsync(CreateProductDto createProductDto, CancellationToken cancellationToken = default);
    Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductDto updateProductDto, CancellationToken cancellationToken = default);
    Task<Result> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default);
}
