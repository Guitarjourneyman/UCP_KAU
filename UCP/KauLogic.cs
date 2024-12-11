using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Student
{
    class KauLogic
    {

        public static void SendMessageTcpAllTrue(Stream stream)
        {
            try
            {
                
                
                // 현재 시간을 "HH:mm:ss.fff" 형식으로 가져오기
                string timeStamp = DateTime.Now.ToString("HH:mm:ss.fff");

               
                // 타임스탬프를 포함한 확인 메시지 전송
                string confirmationMessage = $" [Student]: [{timeStamp}]";
                byte[] confirmationMessageBytes = Encoding.UTF8.GetBytes("<TRUE>" + confirmationMessage);

                
                // string 전송
                stream.Write(confirmationMessageBytes,0,confirmationMessageBytes.Length);

                Console.WriteLine($" Ack message gets sent");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error sending message: {e.Message}");
            }
        }

    }
}
