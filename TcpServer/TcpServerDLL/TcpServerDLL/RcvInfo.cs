using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerDLL
{
    /// <summary>
    /// TCPサーバー受信情報
    /// </summary>
    public class TcpServerRcvInfo
    {
        /// <summary>
        /// 受信時刻
        /// </summary>
        public DateTime Date;

        /// <summary>
        /// 受信元情報
        /// </summary>
        public TcpClient Client;

        /// <summary>
        /// 受信内容
        /// </summary>
        public string RcvBuffer = null;

        /// <summary>
        /// 受信発生
        /// </summary>
        /// <param name="client"></param>
        /// <param name="rcvBuffer"></param>
        public TcpServerRcvInfo(TcpClient client, string rcvBuffer)
        {
            Date = DateTime.Now;
            Client = client;
            RcvBuffer = rcvBuffer;
        }


    }
}
