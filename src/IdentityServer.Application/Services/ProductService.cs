using AutoMapper;
using IdentityServer.Application.DTOs;
using IdentityServer.Application.Interfaces;
using IdentityServer.Domain.Entities;
using IdentityServer.Domain.Exceptions;
using IdentityServer.Domain.Interfaces;
using IdentityServer.Shared.Common;
using IdentityServer.Shared.Models;

namespace IdentityServer.Application.Services;

/// <summary>
/// Service implementation for Product operations
/// </summary>
public class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(IRepository<Product> productRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<Result<PagedList<ProductDto>>> GetAllProductsAsync(PaginationParameters parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await _productRepository.GetAllAsync(cancellationToken);
            var productList = products.Where(p => !p.IsDeleted).ToList();

            var totalCount = productList.Count;
            var pagedProducts = productList
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToList();

            var productDtos = _mapper.Map<List<ProductDto>>(pagedProducts);
            var pagedList = new PagedList<ProductDto>(productDtos, totalCount, parameters.PageNumber, parameters.PageSize);

            return Result<PagedList<ProductDto>>.Success(pagedList);
        }
        catch (Exception ex)
        {
            return Result<PagedList<ProductDto>>.Failure($"Error retrieving products: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);

            if (product == null || product.IsDeleted)
            {
                return Result<ProductDto>.Failure($"Product with ID {id} not found");
            }

            var productDto = _mapper.Map<ProductDto>(product);
            return Result<ProductDto>.Success(productDto);
        }
        catch (Exception ex)
        {
            return Result<ProductDto>.Failure($"Error retrieving product: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> CreateProductAsync(CreateProductDto createProductDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = _mapper.Map<Product>(createProductDto);
            product.Id = Guid.NewGuid();
            product.CreatedAt = DateTime.UtcNow;

            await _productRepository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var productDto = _mapper.Map<ProductDto>(product);
            return Result<ProductDto>.Success(productDto, "Product created successfully");
        }
        catch (Exception ex)
        {
            return Result<ProductDto>.Failure($"Error creating product: {ex.Message}");
        }
    }

    public async Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductDto updateProductDto, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);

            if (product == null || product.IsDeleted)
            {
                return Result<ProductDto>.Failure($"Product with ID {id} not found");
            }

            _mapper.Map(updateProductDto, product);
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var productDto = _mapper.Map<ProductDto>(product);
            return Result<ProductDto>.Success(productDto, "Product updated successfully");
        }
        catch (Exception ex)
        {
            return Result<ProductDto>.Failure($"Error updating product: {ex.Message}");
        }
    }

    public async Task<Result> DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id, cancellationToken);

            if (product == null || product.IsDeleted)
            {
                return Result.Failure($"Product with ID {id} not found");
            }

            product.IsDeleted = true;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success("Product deleted successfully");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Error deleting product: {ex.Message}");
        }
    }
}
