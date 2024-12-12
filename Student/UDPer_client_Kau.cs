using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Data;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Student
{
    
    public class UDPer_client_Kau
    {
        private const int PORT_NUMBER = 49150;
        private const int RECV_PORT_NUMBER = 16000;
        private UdpClient udp = null;
        IAsyncResult ar_ = null;
        private long baseTime = 0;
        private string BROADCAST_ADDRESS = null;
        private IPAddress localIPAddress = null;
        private IPAddress teacherIPAddress = null;
        private Dictionary<int, string> receivedChunks = new Dictionary<int, string>();
        private int expectedChunks = 0;

        private TcpClient client;
        private NetworkStream stream;

        // KAU
        public int TOTAL_PACKETS; // 전체 패킷 수 (필요에 맞게 수정)
        private static int ARRAY_INDEX;
        private static int IGNORED_BITS;
        private static byte[] checkNewMessage;
        private static byte[] lastMessage; // 이전 배열(배열에 변화가 생겼을 때만 ack 전송)
        private const bool MESSAGE_NUM = true;
        private const bool PACKET_NUM = false;
        public static int receivedMessageNum = 1;
        private static int messageLength = Program.MESSAGE_LENGTH;

        public UDPer_client_Kau()
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

        public void Start() {
            Console.WriteLine("UDP Start");
            if (udp != null)
            {
                throw new Exception("Already started, stop first");
            }

            try {
                udp = new UdpClient();

                IPEndPoint localEp = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Trace.WriteLine("buffer size" + udp.Client.ReceiveBufferSize);
                udp.ExclusiveAddressUse = false;
                udp.Client.Bind(localEp);
                
                // KAU: 메세지 패킷 사이즈 계산
                ARRAY_INDEX = CalculateBits(TOTAL_PACKETS, 0);
                IGNORED_BITS = CalculateBits(TOTAL_PACKETS, 1);
                // 패킷 수에 맞는 배열 생성
                checkNewMessage = new byte[ARRAY_INDEX];
                InitializeCheckNewMessage();

                // lastMessage에 저장
                lastMessage = (byte[])checkNewMessage.Clone();

                StartListening();

                
            }
            catch (Exception ex) { 
            
            }
        }

        public void Stop() {
            try
            {
                if (udp != null)
                {
                    Trace.WriteLine("UDP Stopped");
                    udp.Close();
                    udp = null;
                }
            }
            catch (Exception ex) { 
                Trace.WriteLine($"Error stopping UDP client: {ex.Message}");
            }
            
            
        }

        // UDP Receive Begin
        private void StartListening() {
            Trace.WriteLine("UDP Listening Started");
            // ?. 연산자 udp가 null이 아닐때만
            // 여기서는 빈 객체(new object())를 전달하므로, 추가 데이터가 필요 없는 상황
            ar_ = udp?.BeginReceive(Receive, new object());
        }

        // UDP Receive
        private void Receive(IAsyncResult ar) {
            try {
                IPEndPoint ip = new IPEndPoint(IPAddress.Parse(BROADCAST_ADDRESS), PORT_NUMBER);
                byte[] bytes = udp.EndReceive(ar, ref ip);
                string message = Encoding.UTF8.GetString(bytes);
                Trace.WriteLine($"Received message from {ip.Address}, {message}");

                // 다시 수신 대기
                StartListening();

                // KAU 업데이트 예정: udp를 받아서 처리하는 과정

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
                        // 자신이 보낸 UDP Broadcast가 아닌 경우만
                        if //(localIPAddress != null && !ip.Address.Equals(localIPAddress))
                            (localIPAddress != null)
                        {
                            // 메시지의 앞부분 messageLength 글자만 잘라서 표시
                            string truncatedMessage = message.Length > messageLength
                                    ? message.Substring(0, messageLength)
                                    : message;

                            // 메시지 번호와 패킷 번호 추출
                            int message_num = ExtractNumberPart(truncatedMessage, MESSAGE_NUM);
                            int packet_num = ExtractNumberPart(truncatedMessage, PACKET_NUM);

                            // 맞는 메시지 번호가 오면
                            if (receivedMessageNum == message_num)
                            {
                                if (!checkNewMessage.SequenceEqual(lastMessage)) {
                                    // 메시지 출력
                                    // KAU: 변화가 있을 때만 Console로 출력
                                    OnReceiveMessage(truncatedMessage);
                                }

                                // 해당 메세지 번호의 받은 패킷 번호에 맞는 배열의 index를 set
                                SetNewMsgBit(packet_num);
                                

                                // Trace.WriteLine($"Received {message_num} message");
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

        // KAU : 바이트 인덱스 계산하는 메소드
        public static int CalculateBits(int totalPackets, int mode)
        {
            // byte 배열을 사용하기 때문에 패킷 수가 8의 배수가 아니면 사용하지 않는 비트가 생김
            if (mode == 0)
            {
                // mode가 0이면 byte 배열 인덱스 계산
                return (totalPackets + 7) / 8;
            }
            else if (mode == 1)
            {
                // mode가 1이면 무시할 상위 비트 개수 계산
                return (totalPackets % 8 == 0) ? 0 : 8 - (totalPackets % 8);
            }
            else
            {
                throw new ArgumentException("Invalid mode: mode should be 0 or 1");
            }
        }
        // KAU
        // 수신한 패킷 번호에 맞는 bit를 set 시킴
        public static void SetNewMsgBit(int packetNum)
        {
            // packetNum의 위치에 해당하는 비트를 설정(0번째 비트부터 채움)
            int byteIndex = (packetNum - 1) / 8; // 해당 비트가 속한 바이트 인덱스
            int bitIndex = (packetNum - 1) % 8;  // 해당 바이트 내의 비트 위치

            // 해당 바이트 내에서 bitIndex 위치의 비트가 0인지 1인지 확인
            if ((checkNewMessage[byteIndex] & (1 << bitIndex)) == 0)
            {
                // 비트가 0이라면 1로 설정
                checkNewMessage[byteIndex] |= (byte)(1 << bitIndex);
                // Trace.WriteLine($"Set checkNewMessage[{packetNum}]:");
                
            }
            else
            {
                // 이미 비트가 1인 경우
                
            }
        }


        // KAU: UDP로 받은 메시지를 Chunk하는 메소드
        // getLeading이 true면 "_" 기준 앞 숫자 반환, false면 뒤 숫자 반환
        public static int ExtractNumberPart(string input, bool getLeading)
        {
            // '_' 기준으로 앞의 숫자 추출
            int underscoreIndex = input.IndexOf('_');
            string firstNumber = input.Substring(0, underscoreIndex);

            // A가 나오기 전까지의 숫자 추출
            int startIndex = underscoreIndex + 1;
            int endIndex = input.IndexOf('A', startIndex);
            string secondNumber = input.Substring(startIndex, endIndex - startIndex);

           

            // getLeading이 true면 앞 숫자 반환, false면 뒤 숫자 반환
            string numberString = getLeading ? firstNumber : secondNumber;

            // 빈 문자열 확인 후 int로 변환
            return string.IsNullOrEmpty(numberString) ? 0 : int.Parse(numberString);
        }

        // KAU
        // 배열의 모든 비트가 1인지 확인
        private bool AllBitsOne(byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                // 0X : 16진수를 나타내는 접두사
                // 0XFF는 1바이트 8비트에서 표현할 수 있는 가장 큰 값: 11111111
                if (b != 0xFF)
                {
                    // 만약 한 바이트라도 0xFF가 아니라면
                    return false;
                }
            }
            return true;
        }

        // KAU 
        private void InitializeCheckNewMessage()
        {
            Array.Fill(checkNewMessage, (byte)0);

            //byte 배열 초기화(무시해야할 비트들을 모두 1로)
            for (int i = ARRAY_INDEX * 8 - IGNORED_BITS + 1; i <= ARRAY_INDEX * 8; i++)
            {
                SetNewMsgBit(i);
            }
        }

        // KAU
        public void UDP_PacketCheck() {
            try
            {
                // checkNewMessage 배열에 변화가 있는지 확인
                if (!checkNewMessage.SequenceEqual(lastMessage))
                {
                   
                    // 비트맵 출력
                    // PrintByteArrayAsBinary(checkNewMessage);

                    // 모든 비트가 1인지 확인
                    if (AllBitsOne(checkNewMessage))
                    {
                        //TCP Ack 전송
                        SendTCPMessage("");

                        Trace.WriteLine("All packets received.");
                        

                        // checkNewMessage 배열 초기화
                        InitializeCheckNewMessage();

                        // 다음 메시지 준비
                        receivedMessageNum++;
                        Trace.WriteLine($"receivedMessageNum: {receivedMessageNum}");
                    }

                    // 배열 복사
                    lastMessage = (byte[])checkNewMessage.Clone();
                }
            }
            catch (Exception e) {
                Trace.WriteLine($"Error UDPMessage Check: {e.Message}");
            }
         }

        // KAU
        // 배열을 이진수 문자열로 출력하는 헬퍼 메서드
        private static void PrintByteArrayAsBinary(byte[] byteArray)
        {
            foreach (var b in byteArray)
            {
                Console.Write(Convert.ToString(b, 2).PadLeft(8, '0') + " ");
            }
            Console.WriteLine();
        }

        public void ResetBaseTime() { }

        public void ProcessTimeCheckMessage() { }

        private string FormatTimestamp(long timestamp) { return ""; }

        public void Send(string message) { }

        public void SendEx(string message) { }

        public delegate void SendMessageHandler(string message);
        public event SendMessageHandler OnSendMessage;

        public delegate void ReceiveMessageHandler(string message);
        public event ReceiveMessageHandler OnReceiveMessage;

        // UDP Send
        public void SendUnicastMessage(string message) { }

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

        //TCP Ack Send
        public void SendTCPMessage(string message) {
            try
            {
                if (client != null && stream != null && client.Connected)
                {
                    //KAU Ack 보내는 로직 추가
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

        public void Disconnect() {
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

        private void Reconnect() {

            // KAU: 연결을 끊고 나서 재연결을 시도하게 됨

            Disconnect();

            // 재연결을 위한 시도 횟수와 지연 시간 설정
            int retryCount = 5;
            int delay = 2000; // 2초

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
                Thread.Sleep(delay); // 지연 시간 후 재시도
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
    }
}
