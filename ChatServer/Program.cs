// Serveur
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

public class ChatServer
{
    private static List<TcpClient> clients = new List<TcpClient>();
    private static Dictionary<string, string> userProfiles = new Dictionary<string, string>();
    private static Dictionary<TcpClient, string> loggedInUsers = new Dictionary<TcpClient, string>();
    private static Dictionary<string, List<TcpClient>> topics = new Dictionary<string, List<TcpClient>>();
    private static TcpListener? listener;
    private static bool isRunning = false;
    private static string apiKey = "a646cb20255650057994808974874fbf"; // Remplacez par votre clé API valide
    private static readonly string userProfilesFilePath = "userProfiles.json";

    public static void Main(string[] args)
    {
        LoadUserProfiles();

        listener = new TcpListener(IPAddress.Any, 8888);
        listener.Start();
        isRunning = true;
        Console.WriteLine("Chat server started...");

        Thread clientAcceptThread = new Thread(AcceptClients);
        clientAcceptThread.Start();

        Thread weatherBroadcastThread = new Thread(BroadcastWeather);
        weatherBroadcastThread.Start();
    }

    private static void LoadUserProfiles()
    {
        if (File.Exists(userProfilesFilePath))
        {
            string json = File.ReadAllText(userProfilesFilePath);
            userProfiles = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
        }
    }

    private static void SaveUserProfiles()
    {
        string json = JsonConvert.SerializeObject(userProfiles);
        File.WriteAllText(userProfilesFilePath, json);
    }

    private static void AcceptClients()
    {
        while (isRunning)
        {
            TcpClient? client = listener?.AcceptTcpClient();
            if (client != null)
            {
                lock (clients)
                {
                    clients.Add(client);
                }
                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);
            }
        }
    }

    private static void HandleClient(object? clientObject)
    {
        TcpClient? client = clientObject as TcpClient;
        if (client == null)
        {
            return;
        }

        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        int byteCount;

        try
        {
            while ((byteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.ASCII.GetString(buffer, 0, byteCount);
                Console.WriteLine($"Received message: {message}");
                string[] messageParts = message.Split('|');
                if (messageParts.Length > 0)
                {
                    switch (messageParts[0])
                    {
                        case "CREATE_PROFILE":
                            CreateUser(messageParts[1], messageParts[2], client);
                            break;
                        case "LOGIN":
                            AuthenticateUser(messageParts[1], messageParts[2], client);
                            break;
                        case "PRIVATE_MESSAGE":
                            SendPrivateMessage(loggedInUsers[client], messageParts[1], messageParts[2]);
                            break;
                        case "LIST_TOPICS":
                            ListTopics(client);
                            break;
                        case "CREATE_TOPIC":
                            CreateTopic(messageParts[1], client);
                            break;
                        case "JOIN_TOPIC":
                            JoinTopic(messageParts[1], client);
                            break;
                        default:
                            BroadcastMessage(message, client);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client handling error: {ex.Message}");
        }
        finally
        {
            lock (clients)
            {
                clients.Remove(client);
            }
            lock (loggedInUsers)
            {
                if (loggedInUsers.ContainsKey(client))
                {
                    loggedInUsers.Remove(client);
                }
            }
            client.Close();
        }
    }

    private static void BroadcastMessage(string message, TcpClient sender)
    {
        lock (clients)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(message);
            foreach (TcpClient client in clients)
            {
                if (client != sender) // Ne pas renvoyer le message au client qui l'a envoyé
                {
                    NetworkStream? stream = client.GetStream();
                    if (stream != null)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }
    }

    private static void CreateUser(string username, string password, TcpClient client)
    {
        lock (userProfiles)
        {
            if (!userProfiles.ContainsKey(username))
            {
                userProfiles.Add(username, password);
                SaveUserProfiles();
                SendMessageToClient(client, "Profile created successfully.");
                Console.WriteLine($"User '{username}' created successfully.");
            }
            else
            {
                SendMessageToClient(client, "Username already exists.");
                Console.WriteLine($"User '{username}' already exists.");
            }
        }
    }

    private static void AuthenticateUser(string username, string password, TcpClient client)
    {
        lock (userProfiles)
        {
            if (userProfiles.ContainsKey(username) && userProfiles[username] == password)
            {
                lock (loggedInUsers)
                {
                    loggedInUsers[client] = username;
                }
                SendMessageToClient(client, "Login successful.");
                Console.WriteLine($"User '{username}' logged in successfully.");
            }
            else
            {
                SendMessageToClient(client, "Invalid username or password.");
                Console.WriteLine($"Failed login attempt for user '{username}'.");
            }
        }
    }

    private static void SendPrivateMessage(string senderUsername, string recipientUsername, string message)
    {
        lock (clients)
        {
            byte[] buffer = Encoding.ASCII.GetBytes($"Private message from {senderUsername}: {message}");
            foreach (TcpClient client in clients)
            {
                if (loggedInUsers.TryGetValue(client, out string username) && username == recipientUsername)
                {
                    NetworkStream? stream = client.GetStream();
                    if (stream != null)
                    {
                        stream.Write(buffer, 0, buffer.Length);
                        break; // Arrêter après avoir envoyé le message au destinataire
                    }
                }
            }
        }
    }

    private static void ListTopics(TcpClient client)
    {
        lock (topics)
        {
            string topicList = string.Join(", ", topics.Keys);
            Console.WriteLine($"Sending topic list to client: {topicList}");
            SendMessageToClient(client, $"Available topics: {topicList}");
        }
    }


    private static void CreateTopic(string topicName, TcpClient client)
    {
        lock (topics)
        {
            if (!topics.ContainsKey(topicName))
            {
                topics[topicName] = new List<TcpClient> { client };
                SendMessageToClient(client, $"Topic '{topicName}' created and joined successfully.");
                Console.WriteLine($"Topic '{topicName}' created successfully.");
            }
            else
            {
                SendMessageToClient(client, $"Topic '{topicName}' already exists.");
                Console.WriteLine($"Topic '{topicName}' already exists.");
            }
        }
    }

    private static void JoinTopic(string topicName, TcpClient client)
    {
        lock (topics)
        {
            if (topics.TryGetValue(topicName, out List<TcpClient> topicClients))
            {
                if (!topicClients.Contains(client))
                {
                    topicClients.Add(client);
                    SendMessageToClient(client, $"Joined topic '{topicName}' successfully.");
                }
                else
                {
                    SendMessageToClient(client, $"Already joined topic '{topicName}'.");
                }
            }
            else
            {
                SendMessageToClient(client, $"Topic '{topicName}' does not exist.");
            }
        }
    }

    private static void SendMessageToClient(TcpClient client, string message)
    {
        byte[] buffer = Encoding.ASCII.GetBytes(message);
        NetworkStream? stream = client.GetStream();
        if (stream != null)
        {
            stream.Write(buffer, 0, buffer.Length);
        }
    }

    private static async void BroadcastWeather()
    {
        using (HttpClient client = new HttpClient())
        {
            while (isRunning)
            {
                try
                {
                    string url = $"https://api.openweathermap.org/data/2.5/weather?q=Paris&appid={apiKey}&units=metric";
                    string response = await client.GetStringAsync(url);
                    JObject weatherData = JObject.Parse(response);
                    string temperature = weatherData["main"]?["temp"]?.ToString() ?? "unknown";
                    string weatherMessage = $"Weather update: The temperature in Paris is {temperature}°C.";
                    BroadcastMessage(weatherMessage, null); // Envoyer la mise à jour météo à tous les clients
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"Error fetching weather data: {ex.Message}");
                }

                // Attendre 30 minutes avant de récupérer à nouveau la météo
                Thread.Sleep(1800000);
            }
        }
    }
}
