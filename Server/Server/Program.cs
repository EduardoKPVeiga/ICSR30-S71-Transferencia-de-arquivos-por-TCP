using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

class ServerRaw
{
    static List<Socket> clients = new List<Socket>();
    static object lockObj = new object();
    const int port = 2048;
    const int MAX_DATA_SIZE = 1024;

    static void Main(string[] args)
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        serverSocket.Listen(10);
        Console.WriteLine($"Servidor rodando...");

        while (true)
        {
            Socket client = serverSocket.Accept();
            lock (lockObj) clients.Add(client);
            new Thread(() => HandleClient(client)).Start();
        }
    }

    static void HandleClient(Socket client)
    {
        try
        {
            while (true)
            {
                byte[] typeBuf = new byte[1];
                if (client.Receive(typeBuf) == 0) break;

                if (typeBuf[0] == 0x00) break;
                if (typeBuf[0] == 0x02) HandleChat(client);
                if (typeBuf[0] == 0x01) HandleFileRequest(client);
            }
        }
        catch { }
        finally
        {
            lock (lockObj) clients.Remove(client);
            client.Close();
        }
    }

    static void HandleChat(Socket client)
    {
        byte[] lenBuf = ReadExact(client, 2);
        ushort msgLen = BitConverter.ToUInt16(lenBuf, 0);
        byte[] msgBuf = ReadExact(client, msgLen);

        Console.WriteLine($"[CHAT]: {Encoding.UTF8.GetString(msgBuf)}");

        List<byte> pkt = new List<byte> { 0x02 };
        pkt.AddRange(lenBuf);
        pkt.AddRange(msgBuf);
        Broadcast(pkt.ToArray(), client);
    }

    static void HandleFileRequest(Socket client)
    {
        byte[] nameLenBuf = ReadExact(client, 1);
        byte[] nameBuf = ReadExact(client, nameLenBuf[0]);
        string fileName = Encoding.UTF8.GetString(nameBuf);

        if (!File.Exists(fileName)) return;

        SendFileSequence(client, fileName, nameBuf);
    }

    static void SendFileSequence(Socket client, string filePath, byte[] nameRaw)
    {
        FileInfo fi = new FileInfo(filePath);
        int totalPackets = (int)Math.Ceiling((double)fi.Length / MAX_DATA_SIZE);

        List<byte> header = new List<byte> { 0x01, (byte)nameRaw.Length };
        header.AddRange(nameRaw);
        header.AddRange(BitConverter.GetBytes(totalPackets));
        header.AddRange(BitConverter.GetBytes((int)fi.Length));
        client.Send(header.ToArray());
        Thread.Sleep(10);

        using (FileStream fs = File.OpenRead(filePath))
        {
            byte[] buffer = new byte[MAX_DATA_SIZE];
            int bytesRead;
            int packetCount = 1;

            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                List<byte> pkt = new List<byte> { 0x01, (byte)nameRaw.Length };
                pkt.AddRange(nameRaw);
                pkt.AddRange(BitConverter.GetBytes(packetCount));
                pkt.AddRange(BitConverter.GetBytes((ushort)bytesRead));

                byte[] chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                pkt.AddRange(chunk);

                client.Send(pkt.ToArray());
                packetCount++;
                Thread.Sleep(5);
            }
        }

        byte[] hash = CalculateSHA256Bytes(filePath);
        List<byte> hashPkt = new List<byte> { 0x01, (byte)nameRaw.Length };
        hashPkt.AddRange(nameRaw);
        hashPkt.AddRange(BitConverter.GetBytes(totalPackets));
        hashPkt.AddRange(hash);

        client.Send(hashPkt.ToArray());
        Console.WriteLine($"Arquivo enviado: {filePath}");
    }

    static byte[] ReadExact(Socket s, int size)
    {
        byte[] buf = new byte[size];
        int offset = 0;
        while (offset < size)
        {
            int read = s.Receive(buf, offset, size - offset, SocketFlags.None);
            if (read == 0) throw new Exception();
            offset += read;
        }
        return buf;
    }

    static void Broadcast(byte[] data, Socket sender)
    {
        lock (lockObj)
        {
            foreach (var c in clients) if (c != sender && c.Connected) c.Send(data);
        }
    }

    static byte[] CalculateSHA256Bytes(string file)
    {
        using (var sha = SHA256.Create()) using (var s = File.OpenRead(file)) return sha.ComputeHash(s);
    }
}