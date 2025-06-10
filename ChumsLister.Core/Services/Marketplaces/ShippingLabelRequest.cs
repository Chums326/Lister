namespace ChumsLister.Core.Services.Marketplaces
{
    public class ShippingLabelRequest
    {
        public string OrderId { get; set; }
        public string Carrier { get; set; }
        public string ServiceType { get; set; }
        public string TrackingNumber { get; set; }
        public int WeightLbs { get; set; }
        public int WeightOz { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // Add these so that your ViewModel’s PurchaseLabelAsync can set them:
        public string FromPostalCode { get; set; }
        public string ToPostalCode { get; set; }

        // If some older code references FromZip/ToZip, keep aliases:
        public string FromZip
        {
            get => FromPostalCode;
            set => FromPostalCode = value;
        }

        public string ToZip
        {
            get => ToPostalCode;
            set => ToPostalCode = value;
        }
    }
}
