using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllInOneServices.Models
{
    [FirestoreData]
    public class Machine
    {
        [FirestoreProperty]
        public string MachineId { get; set; }

        [FirestoreProperty]
        public string MachineName { get; set; }
    }

    public class SystemInfo
    {
        public string SerialNumber { get; set; } = null;
        public string PcName { get; set; } = null;
    }
}
