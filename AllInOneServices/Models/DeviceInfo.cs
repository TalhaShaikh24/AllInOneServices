using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AllInOneServices.Models
{
    [FirestoreData]
    public class DeviceInfo
    {
        [FirestoreProperty]
        public string MachineId { get; set; } = null;

        [FirestoreProperty]
        public string MacAddress { get; set; }

        [FirestoreProperty]
        public string LocalIpAddress { get; set; }

        [FirestoreProperty]
        public string PcName { get; set; }
        
        [FirestoreProperty]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        [FirestoreProperty]
        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("city")]
        public string City { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("region")]
        public string Region { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("country")]
        public string Country { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("loc")]
        public string Loc { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("org")]
        public string Org { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("postal")]
        public string Postal { get; set; }

        [FirestoreProperty]
        [JsonPropertyName("timezone")]
        public string Timezone { get; set; }
    }
}
