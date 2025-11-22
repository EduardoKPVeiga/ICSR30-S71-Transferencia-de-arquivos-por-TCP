using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class ClientRaw
{
    static bool running = true;

    static void Main(string[] args)
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);

        try
        {
            clientSocket.Connect(remoteEP);
            Console.WriteLine("Conectado ao servidor! Digite suas mensagens:");

            Thread receiveThread = new Thread(() => ReceiveMessages(clientSocket));
            receiveThread.Start();

            while (running)
            {
                Console.Write("Eu: ");
                string msg = Console.ReadLine();

                if (string.IsNullOrEmpty(msg)) continue;
                if (msg == "exit") break;

                byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

                clientSocket.Send(msgBuffer);
            }

            running = false;
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro: " + e.Message);
        }
    }

    static void ReceiveMessages(Socket s)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (running)
            {
                int bytesRead = s.Receive(buffer);

                if (bytesRead > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine("\rServer/Outro: " + msg);
                    Console.Write("Eu: ");
                }
                else
                {
                    running = false;
                    Console.WriteLine("\nDesconectado pelo servidor.");
                }
            }
        }
        catch
        {
            if (running) Console.WriteLine("\nErro na conexão.");
            running = false;
        }
    }
}