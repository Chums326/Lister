namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// Simple view‐model to represent store categories in the right‐hand list.
    /// </summary>
    public class StoreCategoryViewModel
    {
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public bool IsSelected { get; set; }
    }
}
