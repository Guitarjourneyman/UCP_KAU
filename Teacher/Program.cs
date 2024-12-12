
using System;
using System.Diagnostics;
using System.Text;
using Teacher;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Teacher
{
    class Program
    {
        //KAU 
        private static bool startThread = false;
        public const int MAX_MESSAGE_SIZE = 61440;
        public const int PACKET_SIZE = 1024; // 단위 패킷 크기 (1KB)
        private static int receiveNum = 1;
        private static int sendNum = 1;
        private static bool startMsg = false;
        public static int countMsg = 1;
        // UDPer_Kau 클래스 인스턴스 생성
        static UDPer_Kau teacherManager = null;

        static void Main(string[] args)
        {
            // UDPer_Kau 클래스 인스턴스 생성
            teacherManager = new UDPer_Kau();

            // 브로드캐스트 주소 설정 (필요에 따라 변경)
            teacherManager.SetBroadcastAddress("192.168.0.255");

            // 이벤트 핸들러 등록
            teacherManager.OnSendMessage += (message) =>
            {
                Console.WriteLine($"[SEND] Message: {message}");
                sendNum++;
            };

            teacherManager.OnReceiveMessage += (message) =>
            {
                Console.WriteLine($"[RECEIVE][{receiveNum}] Message: {message}");
                receiveNum++;
            };

            // TCP/UDP 서버 시작
            teacherManager.Start();
            Console.WriteLine("Server started. Type 'exit' to quit.");
            Thread sendThread = null;

            // 메시지 송신
            sendThread = new Thread(new ThreadStart(SendPeriodicMessages));

        sendStart:
            Console.WriteLine("UDP Broadcast 시작: ");
            string answer = (Console.ReadLine());
            if (answer.Equals("y") || answer.Equals("Y"))
            {
                // 실질적 시작
                startMsg = true;
                startThread = true;
                goto sendStart;
            }
            else if (answer.Equals("stop"))
            {
                // 메세지 송신 중단 후 Thread 종료
                startThread = false;
                goto sendStart;
            }
            else if (answer.Equals("exit"))
            {
                // 메세지 송신 중단 후 Thread 종료
                startThread = false;

            }
            else if (answer.Equals("start"))
            {
                
                sendThread.Start();

                goto sendStart;
            }
            else
            {
                goto sendStart;
            }
            // 서버 종료
            teacherManager.Stop();
            Console.WriteLine("Server stopped.");
        }

        //UDP Send: 정해진 시간마다 Thread Sleep
        // KAU
        public static void SendPeriodicMessages()
        {
            // 지정된 크기의 연속된 "A" 문자 생성

            StringBuilder messageBuilder = new StringBuilder(MAX_MESSAGE_SIZE);


            while (!startMsg) {
                // 업데이트 예정
                teacherManager.Send(" Request Server Connection ");
                Thread.Sleep(3000);
            }

            while (startThread & startMsg)
            {
                // 모든 클라이언트가 TRUE인지 확인
                if (teacherManager.AllClientReceiveData("<TRUE>"))
                {
                    // 디버그 메시지 출력
                    Trace.WriteLine($"[{Program.countMsg}] {teacherManager.messageNumber} Ack 수신 완료");
                    // Trace.WriteLine($"MessageNumber{teacherManager.messageNumber} is up");
                    // 초기화 후 메시지 번호 증가
                    teacherManager.ResetClientReceivedTimestamps();
                    teacherManager.messageNumber++;
                    countMsg = 1;
                }

                messageBuilder.Append("AAAAA");
                string timestamp = " TeacherTime: " + $"[{DateTime.Now:HH:mm:ss.fff}]";

                messageBuilder.Append(timestamp);
                int len = timestamp.Length;

                for (int i = 0; i < (MAX_MESSAGE_SIZE - 5 - len); i++)
                {
                    messageBuilder.Append('A');
                }
                string message = messageBuilder.ToString();// + timestamp; // 전체 메시지 생성

                // 업데이트 예정
                teacherManager.Send(message);
                countMsg++;
                
                messageBuilder.Clear();

                Thread.Sleep(100); // 100ms 대기
            }
        }


    }



}
