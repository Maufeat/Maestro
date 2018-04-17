using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Timers;

namespace MaestroServer
{
    class Program
    {
        //Console
        private static string Header1 = "MaestroServer";
        private static string Header2 = "Made with <3 by Maufeat";
        private static string Header3 = "Thanks to xupwup's Java Maestro Server";

        //Settings
        private static int port = 8393; // fixed port Maestro is running

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            printHeader();
            Console.ForegroundColor = ConsoleColor.White;

            MaestroServer server = new MaestroServer(port);
        }

        #region ConsoleHeader

        private static void printHeader()
        {
            drawLine(false);
            Console.WriteLine(String.Format("{0," + ((Console.WindowWidth / 2) + Header1.Length / 2) + "}", Header1));
            Console.WriteLine(String.Format("{0," + ((Console.WindowWidth / 2) + Header2.Length / 2) + "}", Header2));
            Console.WriteLine(String.Format("{0," + ((Console.WindowWidth / 2) + Header3.Length / 2) + "}", Header3));
            drawLine(true);
        }

        private static void drawLine(bool _break)
        {
            for (int i = 1; i <= Console.WindowWidth; i++)
            {
                if (i >= Console.WindowWidth && _break)
                {
                    Console.Write("\n");
                    return;
                }
                else if (i >= Console.WindowWidth && !_break)
                {
                    Console.Write("\n");
                    return;
                }
                else
                {
                    Console.Write("=");
                }
            }
        }

        #endregion
    }

    class MaestroServer
    {
        private TcpListener _server;
        private static List<MaestroClient> clients = new List<MaestroClient>();

        public MaestroServer(int port)
        {
            _server = new TcpListener(IPAddress.Any, port);

            var t = Task.Run(() =>
            {
                Log.Print("Maestro Server is running.");
                while (true)
                {
                    _server.Start();
                    var task = _server.AcceptTcpClientAsync();
                    task.Wait();
                    clients.Add(new MaestroClient(task.Result, new byte[4096]));
                }
            });
            t.Wait();
        }
    }


    class MaestroClient
    {
        private TcpClient client;
        private NetworkStream stream;
        private System.Timers.Timer hbTimer;
        private byte[] buffer;

        public MaestroClient(TcpClient client, byte[] buffer)
        {
            this.client = client;
            stream = client.GetStream();
            this.buffer = buffer;

            Log.Print("Client connected. Starting Heartbeat");
            this.hbTimer = new System.Timers.Timer(25000);
            this.hbTimer.Elapsed += async (sender, e) => await HandleHeartbeat(client);
            this.hbTimer.Start();

            receiveData(null);
        }


        private static Task HandleHeartbeat(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] heartbeatData;
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    writer.Write((int)16);
                    writer.Write((int)1);
                    writer.Write((int)MessageType.HEARTBEAT);
                    writer.Write((int)0);
                    heartbeatData = ms.ToArray();
                }
            }
            Log.Print("[SEND] Heartbeat");
            stream.Write(heartbeatData, 0, heartbeatData.Length);
            return Task.CompletedTask;
        }

        private object _lock = new object();
        private async void receiveData(Task<int> result)
        {
            if (result != null)
            {
                lock (_lock)
                {
                    int numberOfBytesRead = result.Result;
                    if (numberOfBytesRead == 0)
                    {
                        onDisconnected();
                        return;
                    }
                    var segmentedArr = new ArraySegment<byte>(buffer, 0, numberOfBytesRead).ToArray();
                    onDataReceived(segmentedArr);
                }

            }
            var task = stream.ReadAsync(buffer, 0, buffer.Length);
            await task.ContinueWith(receiveData);
        }

        private void onDisconnected()
        {
            Log.Print("Client disconnected.");
            hbTimer.Stop();
        }

        private void onDataReceived(byte[] dat)
        {
            Stream stream = new MemoryStream(dat);

            int HeaderLength, Version, Type, DataLength;

            using (BinaryReader reader = new BinaryReader(stream))
            {
                HeaderLength = reader.ReadInt32();
                Version = reader.ReadInt32();
                Type = reader.ReadInt32();
                DataLength = reader.ReadInt32();

                // TODO: Handle more MessageTypes
                switch ((MessageType)Type)
                {
                    case MessageType.HEARTBEAT:
                        Log.Print("[RECV] Heartbeat"); 
                        break;
                    default:
                        Log.Print("[RECV] Unknown Type: " + Type);
                        break;
                }
            }
        }
    }

    public class Log
    {
        public static void Print(string txt)
        {
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " / " + DateTime.Now.ToShortTimeString() + "] " + txt);
        }
    }

    public enum MessageType
    {
        GAMESTART = 0,
        GAMEEND = 1,
        GAMECRASHED = 2,

        EXIT = 3,
        ACK = 5,
        HEARTBEAT = 4,

        GAMECLIENT_LAUNCHED = 8,
        GAMECLIENT_CONNECTED = 10,
        CHATMESSAGE_TO_GAME = 11,
        CHATMESSAGE_FROM_GAME = 12
    }
}
