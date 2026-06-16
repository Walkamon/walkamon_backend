public class ItemTypeResponse
{
    public Guid ItemTypeId { get; set; }
    public string ItemTypeName { get; set; } = null!;

    public bool IsActive { get; set; }
    public int count { get; set; } = 0;
  
}