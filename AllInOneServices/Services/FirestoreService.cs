using Google.Cloud.Firestore;
using AllInOneServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AllInOneServices.Services
{
   
    public class FirestoreService
    {
        private readonly ILogger<FirestoreService> _logger;
        private FirestoreDb _firestoreDb;

        public FirestoreService(ILogger<FirestoreService> logger)
        {
            _logger = logger;

            // Get the path to the service account JSON file in the bin directory
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serviceAccount.json");

            // Set the environment variable for Google Application Credentials
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", filePath);

            // Initialize Firestore
            _firestoreDb = FirestoreDb.Create("scheduledtaskservice");
        }

		public FirestoreService()
		{
		}

		public async Task AddDeviceAsync(Machine deviceInfo)
        {
            try
            {
                var collection = _firestoreDb.Collection("Devices");
                var document = collection.Document(deviceInfo.MachineId);

                // Check if the document already exists
                var snapshot = await document.GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    // Document already exists, handle the situation here if needed
                    // You can throw an exception or log a message
                    _logger.LogInformation("Device with MachineId '{MachineId}' already exists at: {Time}", deviceInfo.MachineId, DateTimeOffset.Now);
                }
                else
                {
                    // Document does not exist, proceed to add it
                    await document.SetAsync(deviceInfo);
					Console.WriteLine("Device registered successfully.");
				}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering device to Firestore");
                throw;
            }
        }

        public async Task AddSystemInfoAsync(DeviceInfo sytemInfo)
        {
            try
            {
                DocumentReference docRef = await _firestoreDb.Collection("SystemInfo").AddAsync(sytemInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding task to Firestore");
                throw;
            }
        }

        public async Task<List<Tasks>> GetPendingTasksAsync(string machineId)
        {
            var tasks = new List<Tasks>();

            try
            {
                // Query tasks where MachineId is empty and Status is "Pending"
                Query query = _firestoreDb.Collection("Tasks")
                                        .WhereEqualTo("MachineId", machineId)
                                        .WhereEqualTo("Status", "Pending");

                QuerySnapshot querySnapshot = await query.GetSnapshotAsync();

                foreach (DocumentSnapshot documentSnapshot in querySnapshot.Documents)
                {
                    if (documentSnapshot.Exists)
                    {
                        var task = documentSnapshot.ConvertTo<Tasks>();
                        tasks.Add(task);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting pending tasks: {ex.Message}");
            }

            return tasks;
        }
        public async Task<Machine> GetDeviceByIdAsync(string machineId)
        {
            var snapshot = await _firestoreDb.Collection("Devices").Document(machineId).GetSnapshotAsync();

            if (snapshot.Exists)
            {
                // Deserialize snapshot into Devices object
                return snapshot.ConvertTo<Machine>();
            }
            else
            {
                // Handle case where document does not exist
                return null;
            }
        }

        public async Task ChangeTaskStatusByTaskIdAsync(string taskId, string newStatus)
        {
            try
            {
                var tasksRef = _firestoreDb.Collection("Tasks");

                // Query to find the task(s) with the specified MachineId
                var query = tasksRef.WhereEqualTo("TaskId", taskId);

                // Execute the query and get the documents
                var querySnapshot = await query.GetSnapshotAsync();

                foreach (var documentSnapshot in querySnapshot.Documents)
                {
                    if (documentSnapshot.Exists)
                    {
                        // Get the reference to the document
                        var docRef = tasksRef.Document(documentSnapshot.Id);

                        // Update the status field of the document
                        await docRef.UpdateAsync(new Dictionary<string, object>
                {
                    {"Status", newStatus}
                });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing task status in Firestore");
                throw;
            }
        }


    }
}
