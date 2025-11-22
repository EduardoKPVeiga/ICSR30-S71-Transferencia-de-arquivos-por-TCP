using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class ServerRaw
{
    static List<Socket> clients = new List<Socket>();
    static object lockObj = new object();

    static void Main(string[] args)
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 8080);

        try
        {
            serverSocket.Bind(localEndPoint);
            serverSocket.Listen(10);

            Console.WriteLine("Servidor (Raw Socket) ouvindo na porta 8080...");

            while (true)
            {
                Socket clientSocket = serverSocket.Accept();

                lock (lockObj) clients.Add(clientSocket);
                Console.WriteLine("Novo cliente conectado.");

                Thread t = new Thread(() => HandleClient(clientSocket));
                t.Start();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    static void HandleClient(Socket client)
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRead = client.Receive(buffer);

                if (bytesRead == 0)
                    break;

                string content = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine("Recebido: " + content);

                Broadcast(buffer, bytesRead, client);
            }
        }
        catch (SocketException)
        {
        }
        finally
        {
            lock (lockObj) clients.Remove(client);
            if (client.Connected) client.Shutdown(SocketShutdown.Both);
            client.Close();
            Console.WriteLine("Cliente desconectado.");
        }
    }

    static void Broadcast(byte[] data, int length, Socket sender)
    {
        lock (lockObj)
        {
            foreach (Socket c in clients)
            {
                if (c != sender && c.Connected)
                {
                    try
                    {
                        c.Send(data, length, SocketFlags.None);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}