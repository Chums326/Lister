namespace ChumsLister.Core.Models
{
    public class CategoryData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool IsLeaf { get; set; }
    }
}