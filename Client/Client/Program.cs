using System.Net;
using System.Net.Sockets;
using System.Text;

class ClientRaw
{
    static bool running = true;

    static Dictionary<string, (int recebidos, int total)> downloadState = new Dictionary<string, (int, int)>();

    static void Main(string[] args)
    {
        IPAddress ip = GetValidIp();
        int port = GetValidPort();

        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            Console.WriteLine($"Conectando a {ip}:{port}...");
            client.Connect(ip, port);

            Console.WriteLine("Conectado com sucesso!");
            Console.WriteLine("Comandos disponíveis:");
            Console.WriteLine(" - chat [mensagem]");
            Console.WriteLine(" - arquivo [nome_do_arquivo.ext]");
            Console.WriteLine(" - sair");
            Console.WriteLine("------------------------------------------------");

            new Thread(() => ReceiveLoop(client)).Start();

            while (running)
            {
                Console.Write("> ");
                string msg = Console.ReadLine();

                if (string.IsNullOrEmpty(msg)) continue;

                if (msg.Equals("sair", StringComparison.OrdinalIgnoreCase))
                {
                    client.Send(new byte[] { 0x00 });
                    running = false;
                    break;
                }
                else if (msg.StartsWith("chat ", StringComparison.OrdinalIgnoreCase))
                {
                    string content = msg.Substring(5);
                    byte[] txt = Encoding.UTF8.GetBytes(content);

                    List<byte> p = new List<byte> { 0x02 };
                    p.AddRange(BitConverter.GetBytes((ushort)txt.Length));
                    p.AddRange(txt);

                    client.Send(p.ToArray());
                }
                else if (msg.StartsWith("arquivo ", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = msg.Substring(8);
                    byte[] nome = Encoding.UTF8.GetBytes(fileName);

                    List<byte> p = new List<byte> { 0x01, (byte)nome.Length };
                    p.AddRange(nome);

                    client.Send(p.ToArray());
                    Console.WriteLine($"Solicitação de download enviada para: {fileName}");
                }
                else
                {
                    Console.WriteLine("Comando desconhecido.");
                }
            }

            if (client.Connected) client.Shutdown(SocketShutdown.Both);
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro fatal: " + ex.Message);
        }
    }

    static IPAddress GetValidIp()
    {
        while (true)
        {
            Console.Write("Digite o IP do servidor: ");
            string input = Console.ReadLine();
            if (IPAddress.TryParse(input, out IPAddress ip))
                return ip;

            Console.WriteLine("IP inválido. Tente novamente.");
        }
    }

    static int GetValidPort()
    {
        while (true)
        {
            Console.Write("Digite a Porta do servidor: ");
            string input = Console.ReadLine();
            if (int.TryParse(input, out int port) && port > 0 && port <= 65535)
                return port;

            Console.WriteLine("Porta inválida. Digite um valor entre 1 e 65535.");
        }
    }

    static void ReceiveLoop(Socket s)
    {
        try
        {
            while (running)
            {
                byte[] typeBuf = new byte[1];
                if (s.Receive(typeBuf) == 0) break;

                if (typeBuf[0] == 0x02)
                {
                    ushort len = BitConverter.ToUInt16(ReadExact(s, 2), 0);
                    string texto = Encoding.UTF8.GetString(ReadExact(s, len));
                    Console.WriteLine($"\r[CHAT] {texto}");
                    Console.Write("> ");
                }
                else if (typeBuf[0] == 0x01)
                {
                    ProcessFilePacket(s);
                }
            }
        }
        catch
        {
            if (running) Console.WriteLine("\nDesconectado do servidor.");
        }
    }

    static void ProcessFilePacket(Socket s)
    {
        int nameLen = ReadExact(s, 1)[0];
        string fileName = Encoding.UTF8.GetString(ReadExact(s, nameLen));
        int counterOrTotal = BitConverter.ToInt32(ReadExact(s, 4), 0);

        if (!downloadState.ContainsKey(fileName))
        {
            int totalPackets = counterOrTotal;
            int fileSize = BitConverter.ToInt32(ReadExact(s, 4), 0);

            downloadState[fileName] = (0, totalPackets);
            File.WriteAllBytes(fileName, new byte[0]);

            Console.WriteLine($"\r[ARQUIVO] Iniciando download: '{fileName}' ({fileSize} bytes)");
            Console.Write("> ");
        }
        else
        {
            var estado = downloadState[fileName];

            if (estado.recebidos < estado.total)
            {
                ushort dataSize = BitConverter.ToUInt16(ReadExact(s, 2), 0);
                byte[] data = ReadExact(s, dataSize);

                using (var fs = new FileStream(fileName, FileMode.Append))
                    fs.Write(data, 0, data.Length);

                downloadState[fileName] = (estado.recebidos + 1, estado.total);
            }
            else
            {
                byte[] serverHash = ReadExact(s, 32);
                string hashStr = BitConverter.ToString(serverHash).Replace("-", "").ToLower();

                Console.WriteLine($"\r[ARQUIVO] Download concluído: '{fileName}'. Hash: {hashStr}");
                downloadState.Remove(fileName);
                Console.Write("> ");
            }
        }
    }

    static byte[] ReadExact(Socket s, int size)
    {
        byte[] buf = new byte[size];
        int offset = 0;
        while (offset < size)
        {
            int r = s.Receive(buf, offset, size - offset, SocketFlags.None);
            if (r == 0) throw new Exception("Socket fechado durante leitura");
            offset += r;
        }
        return buf;
    }
}