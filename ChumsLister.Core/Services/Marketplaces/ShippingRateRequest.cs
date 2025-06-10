namespace ChumsLister.Core.Services.Marketplaces
{
    public class ShippingRateRequest
    {
        // The API needs both Country + PostalCode, so we expose all four properties:
        public string FromCountry { get; set; }       // e.g. "US"
        public string FromPostalCode { get; set; }    // e.g. "49503"
        public string ToCountry { get; set; }         // e.g. "US"
        public string ToPostalCode { get; set; }      // e.g. "89117-2579"

        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int WeightLbs { get; set; }
        public int WeightOz { get; set; }

        // (Optionally keep the old names if some code references them,
        // but now the ViewModel will use FromPostalCode/ToPostalCode.)
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
