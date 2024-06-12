using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class ChatClient
{
    private static TcpClient? client;
    private static NetworkStream? stream;

    public static void Main(string[] args)
    {
        try
        {
            client = new TcpClient("127.0.0.1", 8888);
            stream = client.GetStream();
            Console.WriteLine("Connected to the chat server.");

            Thread readThread = new Thread(ReadMessages);
            readThread.Start();

            Console.WriteLine("Choose an option:");
            Console.WriteLine("1. Create Profile");
            Console.WriteLine("2. Login");
            Console.WriteLine("3. Send Message");
            Console.WriteLine("4. Send Private Message");
            Console.WriteLine("5. List Topics");
            Console.WriteLine("6. Create Topic");
            Console.WriteLine("7. Join Topic");

            string? message;
            while ((message = Console.ReadLine()) != null)
            {
                switch (message)
                {
                    case "1":
                        CreateProfile();
                        break;
                    case "2":
                        Login();
                        break;
                    case "3":
                        SendMessage();
                        break;
                    case "4":
                        SendPrivateMessage();
                        break;
                    case "5":
                        ListTopics();
                        break;
                    case "6":
                        CreateTopic();
                        break;
                    case "7":
                        JoinTopic();
                        break;
                    default:
                        Console.WriteLine("Invalid option. Try again.");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            stream?.Close();
            client?.Close();
        }
    }

    private static void CreateProfile()
    {
        Console.WriteLine("Enter a username:");
        string username = Console.ReadLine();
        Console.WriteLine("Enter a password:");
        string password = Console.ReadLine();
        // Envoyer les données au serveur pour créer un profil
        string message = $"CREATE_PROFILE|{username}|{password}";
        SendMessageToServer(message);
    }

    private static void Login()
    {
        Console.WriteLine("Enter your username:");
        string username = Console.ReadLine();
        Console.WriteLine("Enter your password:");
        string password = Console.ReadLine();
        // Envoyer les informations de connexion au serveur pour authentification
        string loginMessage = $"LOGIN|{username}|{password}";
        SendMessageToServer(loginMessage);
    }

    private static void SendMessage()
    {
        Console.WriteLine("Enter your message:");
        string message = Console.ReadLine();
        SendMessageToServer(message);
    }

    private static void SendPrivateMessage()
    {
        Console.WriteLine("Enter recipient username:");
        string recipientUsername = Console.ReadLine();
        Console.WriteLine("Enter your message:");
        string message = Console.ReadLine();
        // Envoyer le message privé au serveur avec le nom du destinataire
        string privateMessage = $"PRIVATE_MESSAGE|{recipientUsername}|{message}";
        SendMessageToServer(privateMessage);
    }

    private static void ListTopics()
    {
        SendMessageToServer("LIST_TOPICS");
    }

    private static void CreateTopic()
    {
        Console.WriteLine("Enter topic name:");
        string topicName = Console.ReadLine();
        string message = $"CREATE_TOPIC|{topicName}";
        SendMessageToServer(message);
    }

    private static void JoinTopic()
    {
        Console.WriteLine("Enter topic name:");
        string topicName = Console.ReadLine();
        string message = $"JOIN_TOPIC|{topicName}";
        SendMessageToServer(message);
    }

    private static void SendMessageToServer(string message)
    {
        byte[] buffer = Encoding.ASCII.GetBytes(message);
        stream.Write(buffer, 0, buffer.Length);
    }

    private static void ReadMessages()
    {
        byte[] buffer = new byte[1024];
        int byteCount;

        try
        {
            while ((byteCount = stream?.Read(buffer, 0, buffer.Length) ?? 0) > 0)
            {
                string message = Encoding.ASCII.GetString(buffer, 0, byteCount);
                Console.WriteLine(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading messages: {ex.Message}");
        }
    }
}
