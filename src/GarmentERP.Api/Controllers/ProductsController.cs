using GarmentERP.Application.DTOs;
using GarmentERP.Application.Interfaces;
using GarmentERP.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GarmentERP.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductCatalogService _catalogService;

    public ProductsController(IProductCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDto>>> Create([FromBody] CreateProductDto dto, CancellationToken ct)
    {
        var result = await _catalogService.CreateProductAsync(dto, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("matrix")]
    public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> GenerateMatrix([FromBody] CreateVariantMatrixRequest request, CancellationToken ct)
    {
        var result = await _catalogService.GenerateMatrixVariantsAsync(request, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ApiResponse<ProductVariantDto?>>> GetByBarcode(string barcode, CancellationToken ct)
    {
        var result = await _catalogService.FindVariantByBarcodeAsync(barcode, ct);
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<ApiResponse<List<ProductVariantDto>>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _catalogService.SearchVariantsAsync(q, ct);
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<List<CategoryDto>>>> GetCategories(CancellationToken ct)
    {
        return Ok(await _catalogService.GetCategoriesAsync(ct));
    }

    [HttpGet("sizes")]
    public async Task<ActionResult<ApiResponse<List<SizeDto>>>> GetSizes(CancellationToken ct)
    {
        return Ok(await _catalogService.GetSizesAsync(ct));
    }

    [HttpGet("colors")]
    public async Task<ActionResult<ApiResponse<List<ColorDto>>>> GetColors(CancellationToken ct)
    {
        return Ok(await _catalogService.GetColorsAsync(ct));
    }
}
