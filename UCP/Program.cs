using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Student;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Student
{
    class Program
    {
        //KAU 
        public static bool startThread = true;
        private static int receivedNum = 1;
        private static int sendNum = 1;
        public const int MESSAGE_LENGTH = 60;
        // UDPer_Kau 클래스 인스턴스 생성
        static UDPer_client_Kau studentManager = null;

        static void Main(string[] args)
        {
            // UDPer_Kau 클래스 인스턴스 생성
            studentManager = new UDPer_client_Kau();

            // UDP 패킷 수 설정
            studentManager.TOTAL_PACKETS = 61;

            // 브로드캐스트 주소 설정 (필요에 따라 변경)
            studentManager.SetBroadcastAddress("192.168.0.255");

            // 이벤트 핸들러 등록
            studentManager.OnSendMessage += (message) =>
            {
                string timestamp = " [StudentTime]: " + $"[{DateTime.Now:HH:mm:ss.fff}]";
                Console.WriteLine($"[SEND][{sendNum}] Message: {message} {timestamp}");
                receivedNum++;
            };
            
            studentManager.OnReceiveMessage += (message) =>
            {
                string timestamp = " [StudentTime]: " + $"[{DateTime.Now:HH:mm:ss.fff}]";
                Console.WriteLine($"[RECEIVE][{sendNum}] Message: {message} {timestamp}");
                sendNum++;
            };


            // UDP Listening Start
            studentManager.Start();
            Console.WriteLine("Server started. Type 'exit' to quit.");
        sendStart:
            Console.WriteLine("Client 시작: ");
            string answer = (Console.ReadLine());
            if (answer.Equals("y") || answer.Equals("Y"))
            {
               

                Thread UDPCheck = new Thread(new ThreadStart(PeriodicUDP_PacketCheck));
                UDPCheck.Start();

                Console.WriteLine("UDP Receiving");
                goto sendStart;
                 
            }
            else if (answer.Equals("stop"))
            {
                // 업데이트 예정: 미완성
                Console.WriteLine("Student stopped.");
                goto sendStart;
            }
            else if(answer.Equals("exit"))
            {
                // 서버 종료
                studentManager.Stop();
                goto sendStart;
            }

            
        }

        // UDP로 받은 메시지의 패킷이 모두 다 왔는 지 시간마다 확인
        private static void PeriodicUDP_PacketCheck()
        {
            while (true)
            {
                studentManager.UDP_PacketCheck();
                Thread.Sleep(1000);
            }

        }
    }



}
