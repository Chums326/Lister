namespace ChumsLister.WPF.Models
{
    public class StatCardModel
    {
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
        public bool IsPositive { get; set; }
    }
}