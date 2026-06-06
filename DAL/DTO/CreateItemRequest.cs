public class CreateItemRequest
{
    public string ItemName { get; set; } = null!;
    public Guid ItemTypeId { get; set; }
    public string? EffectTypeCode { get; set; }
    public int? EffectValue { get; set; }
    public string? Description { get; set; }
}