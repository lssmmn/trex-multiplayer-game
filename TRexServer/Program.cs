using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TRexShared;

namespace TRexServer
{
    class Program
    {
        static void Main(string[] args)
        {
            TRexServer server = new TRexServer();
            server.Start();
        }
    }

    public class TRexServer
    {
        private TcpListener listener;
        private List<ClientHandler> waitingClients = new List<ClientHandler>();
        private List<GameRoom> activeRooms = new List<GameRoom>();
        private readonly object lockObject = new object();
        private const int PORT = 5000;
        private bool isRunning = false;

        // 데이터베이스 매니저 추가
        private DatabaseManager dbManager;

        public void Start()
        {
            try
            {
                // MySQL 데이터베이스 초기화
                Console.WriteLine("=== 데이터베이스 연결 중 ===");
                dbManager = new DatabaseManager(
                    server: "localhost",      // MySQL 서버 주소
                    database: "trex_game",    // 데이터베이스 이름
                    user: "root",             // MySQL 사용자명
                    password: "your_password" // MySQL 비밀번호
                );
                Console.WriteLine("데이터베이스 연결 성공!\n");

                listener = new TcpListener(IPAddress.Any, PORT);
                listener.Start();
                isRunning = true;

                Console.WriteLine($"=== T-Rex 게임 서버 시작 ===");
                Console.WriteLine($"포트: {PORT}");
                Console.WriteLine($"대기 중...\n");

                // 클라이언트 연결 수락 스레드
                Thread acceptThread = new Thread(AcceptClients);
                acceptThread.IsBackground = true;
                acceptThread.Start();

                // 매칭 처리 스레드
                Thread matchThread = new Thread(ProcessMatching);
                matchThread.IsBackground = true;
                matchThread.Start();

                Console.WriteLine("명령어:");
                Console.WriteLine("  quit - 서버 종료");
                Console.WriteLine("  stats [플레이어명] - 플레이어 통계 조회");
                Console.WriteLine("  leaderboard - 리더보드 조회");
                Console.WriteLine();

                // 콘솔 명령어 처리
                while (isRunning)
                {
                    string command = Console.ReadLine();
                    ProcessCommand(command);
                }

                Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 시작 오류: {ex.Message}");
            }
        }

        private void ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return;

            string[] parts = command.Trim().Split(' ');
            string cmd = parts[0].ToLower();

            switch (cmd)
            {
                case "quit":
                    isRunning = false;
                    break;

                case "stats":
                    if (parts.Length < 2)
                    {
                        Console.WriteLine("사용법: stats [플레이어명]");
                    }
                    else
                    {
                        ShowPlayerStats(parts[1]);
                    }
                    break;

                case "leaderboard":
                    ShowLeaderboard();
                    break;

                default:
                    Console.WriteLine("알 수 없는 명령어입니다.");
                    break;
            }
        }

        private void ShowPlayerStats(string playerName)
        {
            PlayerStats stats = dbManager.GetPlayerStats(playerName);
            if (stats != null)
            {
                Console.WriteLine("\n=== 플레이어 통계 ===");
                Console.WriteLine(stats.ToString());
                Console.WriteLine();

                // 최근 게임 기록
                List<GameRecord> recentGames = dbManager.GetRecentGames(stats.PlayerId, 5);
                if (recentGames.Count > 0)
                {
                    Console.WriteLine("최근 게임:");
                    foreach (var game in recentGames)
                    {
                        Console.WriteLine($"  {game.ToString()}");
                    }
                }
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"플레이어 '{playerName}'을(를) 찾을 수 없습니다.\n");
            }
        }

        private void ShowLeaderboard()
        {
            List<PlayerStats> leaderboard = dbManager.GetLeaderboard(10);
            Console.WriteLine("\n=== 리더보드 (TOP 10) ===");

            if (leaderboard.Count == 0)
            {
                Console.WriteLine("아직 게임 기록이 없습니다.\n");
                return;
            }

            int rank = 1;
            foreach (var player in leaderboard)
            {
                Console.WriteLine($"{rank}. {player.ToString()}");
                rank++;
            }
            Console.WriteLine();
        }

        private void AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ClientHandler handler = new ClientHandler(client, this, dbManager);

                    Thread clientThread = new Thread(handler.HandleClient);
                    clientThread.IsBackground = true;
                    clientThread.Start();

                    Console.WriteLine($"[연결] 새 클라이언트 연결: {client.Client.RemoteEndPoint}");
                }
                catch (Exception ex)
                {
                    if (isRunning)
                        Console.WriteLine($"클라이언트 수락 오류: {ex.Message}");
                }
            }
        }

        private void ProcessMatching()
        {
            while (isRunning)
            {
                try
                {
                    lock (lockObject)
                    {
                        // 대기 중인 클라이언트가 2명 이상이면 매칭
                        if (waitingClients.Count >= 2)
                        {
                            ClientHandler player1 = waitingClients[0];
                            ClientHandler player2 = waitingClients[1];

                            waitingClients.RemoveAt(0);
                            waitingClients.RemoveAt(0);

                            // 게임룸 생성
                            GameRoom room = new GameRoom(player1, player2, this, dbManager);
                            activeRooms.Add(room);

                            Console.WriteLine($"[매칭] 플레이어 매칭 완료! 게임 시작 (룸 ID: {room.RoomId})");

                            // 게임 시작
                            room.StartGame();
                        }
                    }

                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"매칭 처리 오류: {ex.Message}");
                }
            }
        }

        public void AddToQueue(ClientHandler client)
        {
            lock (lockObject)
            {
                if (!waitingClients.Contains(client))
                {
                    waitingClients.Add(client);
                    Console.WriteLine($"[대기열] 클라이언트 추가. 현재 대기: {waitingClients.Count}명");

                    // 대기 중 메시지 전송
                    client.SendMessage(new NetworkMessage(MessageType.WaitingForMatch, "상대를 찾는 중입니다!"));
                }
            }
        }

        public void RemoveFromQueue(ClientHandler client)
        {
            lock (lockObject)
            {
                waitingClients.Remove(client);
            }
        }

        public void RemoveRoom(GameRoom room)
        {
            lock (lockObject)
            {
                activeRooms.Remove(room);
                Console.WriteLine($"[게임종료] 룸 제거 (룸 ID: {room.RoomId}). 활성 룸: {activeRooms.Count}");
            }
        }

        public void Stop()
        {
            isRunning = false;
            listener?.Stop();
            Console.WriteLine("서버 종료됨.");
        }
    }

    // 클라이언트 핸들러
    // 클라이언트 핸들러
    public class ClientHandler
    {
        public TcpClient Client { get; private set; }
        private NetworkStream stream;
        private TRexServer server;
        private DatabaseManager dbManager;
        public GameRoom CurrentRoom { get; set; }
        private bool isConnected = true;

        // 플레이어 정보
        public int PlayerId { get; set; } = -1;
        public string PlayerName { get; set; } = "";

        public ClientHandler(TcpClient client, TRexServer server, DatabaseManager dbManager)
        {
            this.Client = client;
            this.server = server;
            this.dbManager = dbManager;
            this.stream = client.GetStream();
        }

        public void HandleClient()
        {
            try
            {
                byte[] buffer = new byte[1024];
                //Console.WriteLine($"[디버그] HandleClient 시작");

                while (isConnected)
                {
                    //Console.WriteLine($"[디버그] 메시지 대기 중...");
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    //Console.WriteLine($"[디버그] 받은 바이트 수: {bytesRead}");

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    //Console.WriteLine($"[디버그] 받은 원본 메시지: {message}");

                    NetworkMessage msg = NetworkMessage.Deserialize(message);

                    if (msg != null)
                    {
                        //Console.WriteLine($"[디버그] 역직렬화 성공 - Type: {msg.Type}, Data: {msg.Data}");
                        ProcessMessage(msg);
                    }
                    else
                    {
                        //Console.WriteLine($"[디버그] 역직렬화 실패!");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 처리 오류: {ex.Message}");
                Console.WriteLine($"스택 트레이스: {ex.StackTrace}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void ProcessMessage(NetworkMessage msg)
        {
            //Console.WriteLine($"[디버그] ProcessMessage 호출 - Type: {msg.Type}");

            switch (msg.Type)
            {
                case MessageType.SetNickname:
                    SetPlayerNickname(msg.Data);
                    break;

                case MessageType.JoinQueue:
                    if (PlayerId > 0) // 닉네임이 설정된 경우만
                    {
                        server.AddToQueue(this);
                    }
                    else
                    {
                        Console.WriteLine($"[경고] 닉네임 미설정 상태에서 JoinQueue 시도");
                    }
                    break;

                case MessageType.PlayerPosition:
                    CurrentRoom?.RelayPosition(this, msg.Data);
                    break;

                case MessageType.PlayerDied:
                    CurrentRoom?.PlayerDied(this, msg.Data);
                    break;

                case MessageType.RequestRematch:
                    if (PlayerId > 0)
                    {
                        server.AddToQueue(this);
                    }
                    break;
            }
        }

        private void SetPlayerNickname(string nickname)
        {
            //Console.WriteLine($"[디버그] SetPlayerNickname 호출됨 - nickname: '{nickname}'");

            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length < 2 || nickname.Length > 10)
            {
                //Console.WriteLine($"[디버그] 닉네임 유효성 검사 실패");
                SendMessage(new NetworkMessage(MessageType.ConnectionError, "올바른 닉네임을 입력해주세요 (2-10자)"));
                return;
            }

            //Console.WriteLine($"[디버그] DB에 플레이어 생성/조회 시도");
            int playerId = dbManager.GetOrCreatePlayer(nickname);
            //Console.WriteLine($"[디버그] DB 결과 - playerId: {playerId}");

            if (playerId == -1)
            {
                SendMessage(new NetworkMessage(MessageType.ConnectionError, "서버 오류가 발생했습니다."));
                return;
            }

            this.PlayerId = playerId;
            this.PlayerName = nickname;

            Console.WriteLine($"[플레이어] {PlayerName} (ID: {PlayerId}) 닉네임 설정됨");
            SendMessage(new NetworkMessage(MessageType.NicknameAccepted, "닉네임 승인"));
        }

        public void SendMessage(NetworkMessage msg)
        {
            try
            {
                if (Client.Connected && stream != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(msg.Serialize());
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"메시지 전송 오류: {ex.Message}");
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (!isConnected)
                return;

            isConnected = false;
            server.RemoveFromQueue(this);
            CurrentRoom?.PlayerDisconnected(this);

            try
            {
                stream?.Close();
                Client?.Close();
            }
            catch { }

            Console.WriteLine($"[연결해제] {PlayerName} 연결 종료");
        }
    }

    // 게임 룸
    public class GameRoom
    {
        public string RoomId { get; private set; }
        private ClientHandler player1;
        private ClientHandler player2;
        private TRexServer server;
        private DatabaseManager dbManager;
        private Random rand = new Random();
        private bool isGameActive = false;

        // 게임 시간 추적
        private DateTime gameStartTime;

        public GameRoom(ClientHandler p1, ClientHandler p2, TRexServer server, DatabaseManager dbManager)
        {
            this.RoomId = Guid.NewGuid().ToString().Substring(0, 8);
            this.player1 = p1;
            this.player2 = p2;
            this.server = server;
            this.dbManager = dbManager;

            p1.CurrentRoom = this;
            p2.CurrentRoom = this;
        }

        public void StartGame()
        {
            isGameActive = true;
            gameStartTime = DateTime.Now;

            // 매칭 완료 메시지
            player1.SendMessage(new NetworkMessage(MessageType.MatchFound, "상대를 찾았습니다!"));
            player2.SendMessage(new NetworkMessage(MessageType.MatchFound, "상대를 찾았습니다!"));

            Thread.Sleep(500);

            // 동일한 장애물 맵 생성
            GameStartData gameData = GenerateObstacleMap();
            string mapData = gameData.Serialize();

            // 두 플레이어에게 동일한 맵 전송
            player1.SendMessage(new NetworkMessage(MessageType.GameStart, mapData));
            player2.SendMessage(new NetworkMessage(MessageType.GameStart, mapData));

            Console.WriteLine($"[게임시작] 룸 {RoomId}: {player1.PlayerName} vs {player2.PlayerName}");
        }

        private GameStartData GenerateObstacleMap()
        {
            List<ObstacleData> obstacles = new List<ObstacleData>();

            for (int i = 0; i < 3; i++)
            {
                int randomOffset = rand.Next(500, 800);
                ObstacleData obstacle = new ObstacleData(1200, randomOffset, i);
                obstacles.Add(obstacle);
            }

            return new GameStartData(obstacles.ToArray());
        }

        public void RelayPosition(ClientHandler sender, string positionData)
        {
            if (!isGameActive)
                return;

            ClientHandler opponent = (sender == player1) ? player2 : player1;
            opponent?.SendMessage(new NetworkMessage(MessageType.OpponentPosition, positionData));
        }

        public void PlayerDied(ClientHandler sender, string scoreData)
        {
            if (!isGameActive)
                return;

            isGameActive = false;

            // 패배한 플레이어의 점수
            int loserScore = int.Parse(scoreData);

            // 승자와 패자 구분
            ClientHandler loser = sender;
            ClientHandler winner = (sender == player1) ? player2 : player1;

            // 상대방에게 사망 알림
            winner?.SendMessage(new NetworkMessage(MessageType.OpponentDied, scoreData));

            // 게임 지속 시간 계산
            int gameDuration = (int)(DateTime.Now - gameStartTime).TotalSeconds;

            Console.WriteLine($"[게임종료] 룸 {RoomId}: {loser.PlayerName} 사망 (점수: {loserScore})");
            Console.WriteLine($"            게임 시간: {gameDuration}초");

            // ===== DB에 게임 결과 저장 =====
            // 승자의 점수를 받아야 하는데, 일단 간단하게 처리
            // 나중에 양쪽 점수를 모두 받도록 개선 가능

            // 임시: 승자 점수는 패자보다 높다고 가정
            int winnerScore = loserScore + 10; // 임시값

            // player1과 player2 순서에 맞춰 저장
            int player1Score = (loser == player1) ? loserScore : winnerScore;
            int player2Score = (loser == player2) ? loserScore : winnerScore;

            try
            {
                dbManager.SaveGameResult(
                    player1.PlayerId,
                    player2.PlayerId,
                    player1Score,
                    player2Score,
                    gameDuration
                );
                Console.WriteLine($"[DB] 게임 결과 저장 완료 - {player1.PlayerName}({player1Score}) vs {player2.PlayerName}({player2Score})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 게임 결과 저장 실패: {ex.Message}");
            }

            // 잠시 대기 후 방 제거
            Thread.Sleep(1000);
            EndGame();
        }

        public void PlayerDisconnected(ClientHandler disconnected)
        {
            if (!isGameActive)
            {
                EndGame();
                return;
            }

            isGameActive = false;

            ClientHandler opponent = (disconnected == player1) ? player2 : player1;
            if (opponent != null)
            {
                opponent.SendMessage(new NetworkMessage(MessageType.ConnectionError, "상대방이 연결을 끊었습니다."));
            }

            Console.WriteLine($"[연결끊김] 룸 {RoomId}: {disconnected.PlayerName} 연결 끊김");
            EndGame();
        }

        private void EndGame()
        {
            player1.CurrentRoom = null;
            player2.CurrentRoom = null;
            server.RemoveRoom(this);
        }
    }
}