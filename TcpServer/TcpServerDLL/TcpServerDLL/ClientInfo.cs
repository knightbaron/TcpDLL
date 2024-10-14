using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerDLL
{
    /// <summary>
    /// クライアント情報管理用
    /// </summary>
    internal class ClientInfo
    {
        /// <summary>
        /// 接続ソケット
        /// </summary>
        public TcpClient Client;

        /// <summary>
        /// 接続番号
        /// </summary>
        public int No;

        /// <summary>
        /// ポート番号
        /// </summary>
        public int PortNo = 0;

        /// <summary>
        /// IPアドレス
        /// </summary>
        public string IpAddr = "";

        /// <summary>
        /// 接続確認
        /// </summary>
        /// <param name="client"></param>
        /// <param name="no"></param>
        /// <param name="ip"></param>
        /// <param name="portno"></param>
        public ClientInfo(TcpClient client, int no, string ip, int portno)
        {
            Client = client;
            No = no;
            IpAddr = ip;
            PortNo = portno;
        }
    }
}
