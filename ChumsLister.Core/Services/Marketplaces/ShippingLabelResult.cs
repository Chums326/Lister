using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services.Marketplaces
{
    public class ShippingLabelResult
    {
        public bool Success { get; set; }
        public string TrackingNumber { get; set; }
        public string LabelUrl { get; set; }
        public decimal PostageAmount { get; set; }
        public string ErrorMessage { get; set; }
    }
}
