using Microsoft.AspNetCore.Http;

public class UpdateItemRequest
{
    public string ItemName { get; set; } = null!;
    public Guid ItemTypeId { get; set; }
    public string? EffectTypeCode { get; set; }
    public int? EffectValue { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public IFormFile? Image { get; set; }
}