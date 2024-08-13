using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllInOneServices.Models
{
    [FirestoreData]
    public class Tasks
    {
        [FirestoreProperty]
        public string TaskId { get; set; }

        [FirestoreProperty]
        public string MachineId { get; set; }

        [FirestoreProperty]
        public string TaskName { get; set; }

        [FirestoreProperty]
        public string Status { get; set; }

        [FirestoreProperty]
        public string TargetURL { get; set; }

        [FirestoreProperty]
        public string Password { get; set; }

        [FirestoreProperty]
        public DateTime CreatedOn { get; set; }

    }
}
