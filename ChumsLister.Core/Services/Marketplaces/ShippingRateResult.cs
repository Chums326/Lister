using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChumsLister.Core.Services.Marketplaces
{
    public class ShippingRateResult
    {
        public string Carrier { get; set; }
        public string ServiceName { get; set; }
        public decimal Cost { get; set; }
        public int DeliveryDays { get; set; }
    }
}
