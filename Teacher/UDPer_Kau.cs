/**
 * 작성자: 예도훈(KAU)
 * 작성 날짜: 2024-12-09 
 * 
 * 
 * **/



using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Teacher
{
    public class UDPer_Kau
    {

        private const int PORT_NUMBER = 49150;
        private UdpClient udp = null;
        private TcpListener tcpListener;
        private List<TcpClient> clients = new List<TcpClient>();
        // Dictonary는 HashTable의 일종
        private Dictionary<TcpClient, NetworkStream> clientStreams = new Dictionary<TcpClient, NetworkStream>();
        private Dictionary<TcpClient, StringBuilder> clientMessageBuffers = new Dictionary<TcpClient, StringBuilder>();
        private Dictionary<TcpClient, string> clientReceivedTimestamps = new Dictionary<TcpClient, string>(); // 클라이언트 데이터 수신 타임스탬프

        //IAsyncResult를 통해 작업 상태 및 결과 전달
        IAsyncResult ar_ = null;

        
        private string BROADCAST_ADDRESS = null;
        private IPAddress localIPAddress = null;

        // KAU
        public int messageNumber = 1;
        // 업데이트 예정: Parameter로 수정 main에서 const로 수정
        private int maxMessageSize = Program.MESSAGE_SIZE;

        public UDPer_Kau()
        {
            try {
                localIPAddress = GetLocalIPAddress();

            }
            catch(Exception ex) {
                Trace.WriteLine($"Error getting local IP Address: {ex.Message}");
            }
        }

        public void SetBroadcastAddress(string broadcastAddress) {
            BROADCAST_ADDRESS = broadcastAddress;
        }

        public void Start() {

            if (tcpListener != null) {
                throw new Exception("Already started stop first");
            }

            try { 
                tcpListener = new TcpListener(IPAddress.Any, PORT_NUMBER);
                tcpListener.Start();
                Trace.WriteLine("TCP listener started");
                StartListening();
            }
            catch (Exception ex) {
                Trace.WriteLine($"Error starting TCP listener: {ex.Message}");
            }
        }
        public void Stop() {
            // udp instance stop
            try
            {
                if (udp != null)
                {
                    udp.Close();
                    udp = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error stopping UDP client: {ex.Message}");
            }
            // tcp server stop
            try
            {
                foreach (var client in clients.ToArray())
                {
                    DisconnectClient(client);
                }
                clients.Clear();
                clientStreams.Clear();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error disconnecting clients: {ex.Message}");
            }

            try
            {
                tcpListener?.Stop();
                tcpListener = null;

                foreach (var client in clients)
                {
                    if (clientStreams.TryGetValue(client, out var stream))
                    {
                        stream?.Close();
                    }
                    client.Close();
                }
                clients.Clear();
                clientStreams.Clear();

                Trace.WriteLine("TCP listener stopped");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error stopping TCP listener: {ex.Message}");
            }
        }

        public void StartListening()
        {
            try {
                tcpListener.BeginAcceptTcpClient(AcceptTcpClient, null);
                Trace.WriteLine("Start Listening for TCP connections");
            }
            catch(Exception ex){
                Trace.WriteLine($"Error starting listening: {ex.Message}");
            }
        }
        public void AcceptTcpClient(IAsyncResult ar)
        {
            try
            {
                TcpClient client = tcpListener.EndAcceptTcpClient(ar);
                clients.Add(client);
                // Byte를 읽기위해 Stream을 받아 Dictionary에 저장해둠 
                NetworkStream stream = client.GetStream();
                clientStreams[client] = stream;
                Trace.WriteLine("Client connected");

                StartReading(client);
                // 작업을 완료했으면, 다시 연결 요청 대기 상태
                StartListening();
            }
            catch (Exception ex) {
                Trace.WriteLine($"Error accepting client: {ex.Message}");
            }
        }

        // TCP Ack Receive
        private void StartReading(TcpClient client) {
            NetworkStream stream = clientStreams[client];
            byte[] buffer = new byte[1024];
            stream.BeginRead(buffer, 0, buffer.Length, ar => ReadCallback(ar, client), buffer);
        }

        private void ReadCallback(IAsyncResult ar, TcpClient client)
        {
            try {
                NetworkStream stream = clientStreams[client];
                int bytesRead = stream.EndRead(ar);
                // KAU: true/false로만 읽도록 수정

                // 네트워크 스트림 생성
           
                if (bytesRead > 0)
                {

                    //버퍼 데이터 가져오기                    
                    byte[] buffer = (byte[])ar.AsyncState;
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    // **수신 메시지 출력**
                    string receviedTimestamp = " [Teacher]: " + $"[{DateTime.Now:HH:mm:ss.fff}]";
                    OnReceiveMessage?.Invoke(receivedMessage + receviedTimestamp);

                    // "<TRUE>" 추출 및 확인
                    if (receivedMessage.Contains("<TRUE>"))
                    {
                        clientReceivedTimestamps[client] = "<TRUE>";
                    }
                    // 모든 클라이언트가 TRUE인지 확인
                    if (AllClientReceiveData("<TRUE>")) { 
                        messageNumber++;
                        Trace.WriteLine($"MessageNumber{messageNumber} is up");
                        // 메시지 번호 증가 후 초기화
                        ResetClientReceivedTimestamps();
                    }
                    // 다음 데이터를 비동기로 읽기
                    StartReading(client);


                }
                else { 
                    DisconnectClient(client);
                }
                
            }
            catch (Exception ex) {
                Trace.WriteLine($"Error reading data: {ex.Message}");
                DisconnectClient(client);
            }
        }

        private void DisconnectClient(TcpClient client) {
            try
            {
                if (clientStreams.TryGetValue(client, out NetworkStream stream))
                {
                    stream.Close();  // 클라이언트 스트림을 명시적으로 닫습니다.
                }

                client.Close();  // 클라이언트 소켓을 명시적으로 닫습니다.

                clientStreams.Remove(client);
                clients.Remove(client);

                // 클라이언트 메시지 버퍼를 삭제합니다.
                clientMessageBuffers.Remove(client);

                Trace.WriteLine($"Client disconnected: {client.Client.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error disconnecting client: {ex.Message}");
            }
        }

        public void ResetClientReceivedTimestamps() {

            foreach (var client in clients) {
                clientReceivedTimestamps[client] = null;
            }
        }

        // KAU 업데이트 예정: 메시지를 받은 다음 처리 방식
        public bool AllClientReceiveData(string timestamp) {

            foreach (var receivedTimestamp in clientReceivedTimestamps.Values)
            {
                if (receivedTimestamp != timestamp) {
                    return false;
                }
            }
            return true; }

        // UDP Receive
        public void Receive(IAsyncResult ar) {
            try
            {
                IPEndPoint ip = new IPEndPoint(IPAddress.Any, PORT_NUMBER);
                byte[] bytes = udp.EndReceive(ar, ref ip);
                string message = Encoding.UTF8.GetString(bytes);

                StartListening();

                if (OnReceiveMessage != null)
                {
                    if (localIPAddress != null && !ip.Address.Equals(localIPAddress))
                    {
                        OnReceiveMessage(message);
                    }
                }
            }
            catch (SocketException se) {
                Trace.WriteLine($"SocketException: {se.Message}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception: {ex.Message}");
            }
        }

        
        // UDP Send
        
        public void Send(string message) {
            

            if (string.IsNullOrEmpty(BROADCAST_ADDRESS)) {
                Trace.WriteLine("Broadcast address is not set.");
                return;
            }

            try {
                using (UdpClient client = new UdpClient()) { 
                    IPEndPoint ip = new IPEndPoint(IPAddress.Parse(BROADCAST_ADDRESS), PORT_NUMBER);
                    client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    //KAU
                    
                    byte[] bytes = Encoding.UTF8.GetBytes(message);

                    if (bytes.Length > maxMessageSize)
                    {
                        Trace.WriteLine($"Message size ({bytes.Length} bytes) exceeds the maximum limit of {maxMessageSize} bytes. Truncating the message.");
                        return;
                    }

                    // KAU : 메세지를 Packet으로 Chunk해서 Send -> 필요하면 메소드화 인자를 여러개 넘겨줘야해서 따로 만들지 않음
                    else if (bytes.Length > 1500 && bytes.Length <= maxMessageSize) {

                        int offset = 0;
                        int packetCount = 0; // 패킷 번호 카운터

                        // 메시지가 1024바이트를 초과할 경우, 1024바이트 단위로 분할하여 전송
                        while (offset < bytes.Length)
                        {
                            // 한 패킷의 길이: 1024바이트(-10은 헤더 길이)와 (메시지 전체 길이 - offset)의 최솟값
                            int length = Math.Min(Program.PACKET_SIZE - 10, bytes.Length - offset);
                            byte[] buffer = new byte[length + 10]; // 패킷 번호를 저장할 공간(헤더) 추가

                            // 패킷 번호를 헤더에 삽입
                            string packetHeader = $"{messageNumber}_{packetCount + 1}"; // 1부터 시작하는 패킷 번호
                            byte[] headerBytes = Encoding.UTF8.GetBytes(packetHeader);
                            Array.Copy(headerBytes, 0, buffer, 0, headerBytes.Length); // 헤더를 버퍼에 복사

                            // 메시지 데이터를 버퍼에 복사
                            Array.Copy(bytes, offset, buffer, headerBytes.Length, length);

                            // UDP 패킷 생성 및 전송
                            int ret = client.Send(buffer, buffer.Length, ip);
                            offset += length;
                            packetCount++; // 패킷 번호 증가

                            // 디버그 메시지 출력
                            Trace.WriteLine($"{packetCount}번째 패킷 전송 완료");
                            // KAU
                            if (ret != 0 && ret != buffer.Length)
                            {
                                Trace.WriteLine($"Message not fully sent. Sent {ret} bytes out of {buffer.Length}.");
                            }
                            else
                            {
                                // 메시지의 앞부분 30글자만 잘라서 표시
                                string truncatedMessage = Encoding.UTF8.GetString(buffer, 0, Math.Min(buffer.Length, 30));


                                // OnSendMessage(truncatedMessage);
                                Trace.WriteLine("Message sent successfully.");
                            }
                            
                        }

                    }

                   
                    
                    

                }    
            }
            catch (Exception ex) {
                Trace.WriteLine($"Error sending message: {ex.Message}");
            }            
        }

        
        /*Handler를 Main에서 호출하여 Send/Receive 사용*/
        public delegate void SendMessageHandler(string message);
        public event SendMessageHandler OnSendMessage;

        public delegate void ReceiveMessageHandler(string message);
        public event ReceiveMessageHandler OnReceiveMessage;








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
