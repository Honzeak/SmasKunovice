using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;

public class DronetagMqttConsumer
{
    // Class to hold the JSON/ODID data structure, mirroring the document
    public class OdidData
    {
        public BasicId[]? BasicID { get; set; }
        public Location? Location { get; set; }
        public SelfId? SelfID { get; set; }
        public SystemData? System { get; set; }
        public OperatorId? OperatorID { get; set; }
    }

    public class BasicId
    {
        public int UAType { get; set; }
        public int IDType { get; set; }
        public string? UASID { get; set; }
    }

    public class Location
    {
        public int Status { get; set; }
        public float? Longitude { get; set; }
        public float? Latitude { get; set; }
        public int? Direction { get; set; }
        public float? SpeedHorizontal { get; set; }
        public float? SpeedVertical { get; set; }
        public float? AltitudeBaro { get; set; }
        public float? AltitudeGeo { get; set; }
        public int HeightType { get; set; }
        public float? Height { get; set; }
        public int HorizAccuracy { get; set; }
        public int VertAccuracy { get; set; }
        public int BaroAccuracy { get; set; }
        public int SpeedAccuracy { get; set; }
        public int TSAccuracy { get; set; }
        public string? Timestamp { get; set; }
    }

    public class SelfId
    {
        public int DescType { get; set; }
        public string? Desc { get; set; }
    }

    public class SystemData
    {
        public int OperatorLocationType { get; set; }
        public int ClassificationType { get; set; }
        public float? OperatorLatitude { get; set; }
        public float? OperatorLongitude { get; set; }
        public int AreaCount { get; set; }
        public int AreaRadius { get; set; }
        public float? AreaCeiling { get; set; }
        public float? AreaFloor { get; set; }
        public int CategoryEU { get; set; }
        public int ClassEU { get; set; }
        public float? OperatorAltitudeGeo { get; set; }
        public string? Timestamp { get; set; }
    }

    public class OperatorId
    {
        public int OperatorIdType { get; set; }
        public string? OperatorId { get; set; }
    }
    public class ScoutData
    {
        public int rssi { get; set; }
        public string[]? tech { get; set; }
        public int recv_id { get; set; }
        public int module_id { get; set; }
        public int module_type { get; set; }
        public int msg_type { get; set; }
        public OdidData? odid { get; set; }
    }

    public static async Task RunAsync()
    {
        // Create a new MQTT client
        var factory = new MqttFactory();
        using (var client = factory.CreateMqttClient())
        {
            // Configure MQTT client options (replace with your broker details)
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("broker.hivemq.com", 1883) // Use a public broker for testing
                .WithCleanSession() // Start with a clean session
                .Build();

            // Set up event handlers for connected and message received
            client.ConnectedAsync += async e =>
            {
                Console.WriteLine("Connected to MQTT broker.");
                // Subscribe to the topic (replace with your Dronetag topic)
                await client.SubscribeAsync("#"); //changed from "dronetag/scout" to "#" to receive all the data
                Console.WriteLine("Subscribed to topic '#'.");
            };

            client.DisconnectedAsync += async e =>
            {
                Console.WriteLine("Disconnected from MQTT broker.  Attempting to reconnect in 5 seconds.");
                await Task.Delay(5000); //wait 5 seconds before reconnecting
                try
                {
                    await client.ConnectAsync(options, CancellationToken.None); //attempt to reconnect
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error on reconnect: {ex.Message}");
                }

            };

            client.MessageReceivedAsync += e =>
            {
                try
                {
                    // Handle received MQTT messages
                    Console.WriteLine("Received message:");
                    string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    Console.WriteLine(message);

                    // Deserialize the JSON payload into the ScoutData object
                    ScoutData? scoutData = JsonSerializer.Deserialize<ScoutData>(message);

                    if (scoutData != null)
                    {
                        // Process the received data
                        ProcessScoutData(scoutData);
                    }
                    else
                    {
                        Console.WriteLine("Failed to deserialize MQTT message.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");
                }
                return Task.CompletedTask;
            };

            // Attempt to connect to the MQTT broker
            try
            {
                await client.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error connecting to MQTT broker: {e.Message}");
                return; // Exit if connection fails
            }

            // Keep the application running to receive messages
            Console.WriteLine("Listening for MQTT messages. Press Enter to exit.");
            Console.ReadLine();

            // Disconnect from the broker
            try
            {
                await client.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disconnecting: {ex.Message}");
            }
        }
    }

    static void ProcessScoutData(ScoutData data)
    {
        // Example: Print some of the received data
        Console.WriteLine("------------------------------------------");
        Console.WriteLine($"RSSI: {data.rssi}");
        Console.WriteLine($"Technology: {string.Join(", ", data.tech ?? Array.Empty<string>())}"); // null check
        Console.WriteLine($"Module ID: {data.module_id}");
        Console.WriteLine($"Message Type: {data.msg_type}");

        if (data.odid?.Location != null)
        {
            Console.WriteLine($"  Latitude: {data.odid.Location.Latitude}");
            Console.WriteLine($"  Longitude: {data.odid.Location.Longitude}");
            Console.WriteLine($"  Altitude (Baro): {data.odid.Location.AltitudeBaro}");
            Console.WriteLine($"  Timestamp: {data.odid.Location.Timestamp}");
        }
        if (data.odid?.BasicID != null)
        {
            foreach (var basicId in data.odid.BasicID)
            {
                Console.WriteLine($"  UAS ID: {basicId.UASID}");
                Console.WriteLine($"    UA Type: {basicId.UAType}");
                Console.WriteLine($"    ID Type: {basicId.IDType}");
            }
        }
        // Add more data processing here as needed
    }

    public static void Main(string[] args)
    {
        RunAsync().Wait();
    }
}

