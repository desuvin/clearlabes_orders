using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ShopifySharp.Filters
{
    public class OrderFilterWithName : ListFilter<Order>
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("status")]
        public string Status { get; internal set; }

        [JsonProperty("fulfillment_status")]
        public string FullfilmentStatus { get; set; }
    }
}
