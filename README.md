## 1.プロジェクトの概要
- TCPのServerおよびClientライブラリのためのソース

## 2.開発環境
- VisualStudio 2022
- .NET FrameWork 4.8

## 3.提供
- TcpServer
- TcpClient

### 3.1 TcpServer
TCPサーバーのためのライブラリ<br>
接続はすべて受け入れ<br>
KeepAliveなし<br>
送信はIPアドレスとポート番号を指定<br>
※クライアントリストに存在しない場合は送信しない<br>