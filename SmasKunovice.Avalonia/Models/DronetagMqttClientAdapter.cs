using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace SmasKunovice.Avalonia.Models;

public class DronetagMqttClientAdapter : IDroneTagClient
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _connectOptions;
    public event IDroneTagClient.DronetagDataReceivedEventHandler? MessageReceived;

    public DronetagMqttClientAdapter(string host, int port, string username, string password)
    {
        _client = new MqttClientFactory().CreateMqttClient();
        _connectOptions = new MqttClientOptionsBuilder()
                .WithTcpServer("broker.hivemq.com", 1883) // Use a public broker for testing
                .WithCleanSession() // Start with a clean session
                .Build();
    }

    public async Task ConnectAsync()
    {
        await _client.ConnectAsync(_connectOptions);
    }
    public static async Task RunAsync()
    {
        // Create a new MQTT client
        var factory = new MqttClientFactory();
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

            client.ApplicationMessageReceivedAsync += e =>
            {
                try
                {
                    // Handle received MQTT messages
                    Console.WriteLine("Received message:");
                    string message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
                    Console.WriteLine(message);

                    // Deserialize the JSON payload into the ScoutData object
                    ScoutData? scoutData = JsonSerializer.Deserialize<ScoutData>(message, ScoutData.SerializerOptions);

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
        Console.WriteLine($"RSSI: {data.Rssi}");
        Console.WriteLine($"Technology: {string.Join(", ", data.Tech ?? Array.Empty<string>())}"); // null check
        Console.WriteLine($"Module ID: {data.ModuleId}");
        Console.WriteLine($"Message Type: {data.MsgType}");

        if (data.Odid?.Location != null)
        {
            Console.WriteLine($"  Latitude: {data.Odid.Location.Latitude}");
            Console.WriteLine($"  Longitude: {data.Odid.Location.Longitude}");
            Console.WriteLine($"  Altitude (Baro): {data.Odid.Location.AltitudeBaro}");
            Console.WriteLine($"  Timestamp: {data.Odid.Location.Timestamp}");
        }
        if (data.Odid?.BasicId != null)
        {
            foreach (var basicId in data.Odid.BasicId)
            {
                Console.WriteLine($"  UAS ID: {basicId.UasId}");
                Console.WriteLine($"    UA Type: {basicId.UaType}");
                Console.WriteLine($"    ID Type: {basicId.IdType}");
            }
        }
        // Add more data processing here as needed
    }

    // public static void Main(string[] args)
    // {
    //     RunAsync().Wait();
    // }
    public void Dispose()
    {
        
    }

}