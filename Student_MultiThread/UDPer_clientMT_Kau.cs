using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Student
{
    public class UDPer_clientMT_Kau
    {
        private const int PORT_NUMBER = 49150;
        private const int RECV_PORT_NUMBER = 16000;
        private UdpClient udp = null;
        private long baseTime = 0;
        private string BROADCAST_ADDRESS = null;
        private IPAddress localIPAddress = null;
        private IPAddress teacherIPAddress = null;
        private Dictionary<int, string> receivedChunks = new Dictionary<int, string>();
        private int expectedChunks = 0;
        private TcpClient client;
        private NetworkStream stream;
        
        // KAU
        public int packentNum = Program.TOTAL_PACKETS;
        private static int ARRAY_INDEX;
        private static int IGNORED_BITS;
        private static byte[] checkMessageChunk;
        private static byte[] lastMessageChunk;
        private const bool MESSAGE_NUM = true;
        private const bool PACKET_NUM = false;
        public static int receivedMessageNum = 1;
        private static int messageLength = Program.MESSAGE_LENGTH;
        private Thread receiveThread; // MultiThread
        private bool isRunning = false; // MultiThread

        public UDPer_clientMT_Kau()
        {
            try
            {
                localIPAddress = GetLocalIPAddress();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error getting local IP address: {ex.Message}");
            }
        }

        public void SetBroadcastAddress(string broadcastAddress)
        {
            BROADCAST_ADDRESS = broadcastAddress;
        }

        public void Start()
        {
            Console.WriteLine("UDP Start");
            if (udp != null)
            {
                throw new Exception("Already started, stop first");
            }

            try
            {
                udp = new UdpClient();

                IPEndPoint localEp = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Trace.WriteLine("buffer size" + udp.Client.ReceiveBufferSize);
                udp.ExclusiveAddressUse = false;
                udp.Client.Bind(localEp);

                ARRAY_INDEX = CalculateBits(packentNum, 0);
                IGNORED_BITS = CalculateBits(packentNum, 1);
                checkMessageChunk = new byte[ARRAY_INDEX];
                InitializeCheckNewMessage();
                lastMessageChunk = (byte[])checkMessageChunk.Clone();

                StartListening();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error starting UDP client: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                isRunning = false; // MultiThread
                receiveThread?.Join(); // MultiThread

                if (udp != null)
                {
                    Trace.WriteLine("UDP Stopped");
                    udp.Close();
                    udp = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error stopping UDP client: {ex.Message}");
            }
        }

        // MultiThread: Start listening with a separate thread
        private void StartListening()
        {
            Trace.WriteLine("UDP Listening Started");
            isRunning = true; // MultiThread
            receiveThread = new Thread(ReceiveLoop); // MultiThread
            receiveThread.Start(); // MultiThread
        }

        // MultiThread: Loop for receiving messages
        private void ReceiveLoop()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint ip = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = udp.Receive(ref ip); // Blocking call
                    string message = Encoding.UTF8.GetString(bytes);
                    Trace.WriteLine($"Received message from {ip.Address}, {message}");

                    if (teacherIPAddress == null)
                    {
                        teacherIPAddress = ip.Address;
                        Trace.WriteLine($"Teacher ID: {teacherIPAddress}");
                        ConnectToServer();
                    }
                    else
                    {
                        if (OnReceiveMessage != null)
                        {
                            if (localIPAddress != null)
                            {
                                string truncatedMessage = message.Length > messageLength
                                    ? message.Substring(0, messageLength)
                                    : message;

                                int message_num = ExtractNumberPart(truncatedMessage, MESSAGE_NUM);
                                int packet_num = ExtractNumberPart(truncatedMessage, PACKET_NUM);

                                if (receivedMessageNum == message_num)
                                {
                                    OnReceiveMessage(truncatedMessage);
                                    SetNewMsgBit(packet_num);
                                }
                                else
                                {
                                    Trace.WriteLine("Received wrong message");
                                }
                            }
                        }
                    }
                }
                catch (SocketException se)
                {
                    Trace.WriteLine($"SocketException: {se.Message}");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Exception: {ex.Message}");
                }
            }
        }

        public static int CalculateBits(int totalPackets, int mode)
        {
            if (mode == 0)
            {
                return (totalPackets + 7) / 8;
            }
            else if (mode == 1)
            {
                return (totalPackets % 8 == 0) ? 0 : 8 - (totalPackets % 8);
            }
            else
            {
                throw new ArgumentException("Invalid mode: mode should be 0 or 1");
            }
        }

        public static void SetNewMsgBit(int packetNum)
        {
            int byteIndex = (packetNum - 1) / 8;
            int bitIndex = (packetNum - 1) % 8;

            if ((checkMessageChunk[byteIndex] & (1 << bitIndex)) == 0)
            {
                checkMessageChunk[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        public static int ExtractNumberPart(string input, bool getLeading)
        {
            int underscoreIndex = input.IndexOf('_');
            string firstNumber = input.Substring(0, underscoreIndex);

            int startIndex = underscoreIndex + 1;
            int endIndex = input.IndexOf('A', startIndex);
            string secondNumber = input.Substring(startIndex, endIndex - startIndex);

            string numberString = getLeading ? firstNumber : secondNumber;

            return string.IsNullOrEmpty(numberString) ? 0 : int.Parse(numberString);
        }

        private bool AllBitsOne(byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                if (b != 0xFF)
                {
                    return false;
                }
            }
            return true;
        }

        private void InitializeCheckNewMessage()
        {
            Array.Fill(checkMessageChunk, (byte)0);

            for (int i = ARRAY_INDEX * 8 - IGNORED_BITS + 1; i <= ARRAY_INDEX * 8; i++)
            {
                SetNewMsgBit(i);
            }
        }

        // KAU
        public void UDP_PacketCheck()
        {
            try
            {
                // checkMessageChunk 배열에 변화가 있는지 확인
                if (!checkMessageChunk.SequenceEqual(lastMessageChunk))
                {

                    // 비트맵 출력
                    // PrintByteArrayAsBinary(checkMessageChunk);

                    // 모든 비트가 1인지 확인
                    if (AllBitsOne(checkMessageChunk))
                    {
                        //TCP Ack 전송
                        SendTCPMessage("");

                        Trace.WriteLine("All packets received.");


                        // checkMessageChunk 배열 초기화
                        InitializeCheckNewMessage();

                        // 다음 메시지 준비
                        receivedMessageNum++;
                        Trace.WriteLine($"receivedMessageNum: {receivedMessageNum}");
                    }

                    // 배열 복사
                    lastMessageChunk = (byte[])checkMessageChunk.Clone();
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Error UDPMessage Check: {e.Message}");
            }
        }

        public void ConnectToServer()
        {
            try
            {
                client = new TcpClient();
                client.Connect(teacherIPAddress, PORT_NUMBER);
                stream = client.GetStream();
                Trace.WriteLine("Connected to server");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error connecting to server: {ex.Message}");
            }
        }

        public void SendTCPMessage(string message)
        {
            try
            {
                if (client != null && stream != null && client.Connected)
                {
                    KauLogic.SendMessageTcpAllTrue(stream);
                    Trace.WriteLine($"TCP message sent to server .");
                }
                else
                {
                    Trace.WriteLine("Client is not connected to server. Attempting to reconnect...");
                    Reconnect();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error sending TCP message: {ex.Message}");
                Reconnect();
            }
        }

        public void Disconnect()
        {
            try
            {
                stream?.Close();
                client?.Close();
                client = null;
                stream = null;
                Trace.WriteLine("Disconnected from server");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error disconnecting from server: {ex.Message}");
            }
        }

        private void Reconnect()
        {
            Disconnect();

            int retryCount = 5;
            int delay = 2000;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    Trace.WriteLine($"Reconnecting attempt {i + 1}/{retryCount}...");
                    ConnectToServer();
                    if (client != null && client.Connected)
                    {
                        Trace.WriteLine("Reconnected to server");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Reconnection attempt {i + 1} failed: {ex.Message}");
                }
                Thread.Sleep(delay);
            }

            Trace.WriteLine("Failed to reconnect after multiple attempts.");
        }

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public delegate void ReceiveMessageHandler(string message);
        public event ReceiveMessageHandler OnReceiveMessage;

        public delegate void SendMessageHandler(string message);
        public event SendMessageHandler OnSendMessage;
    }
}
