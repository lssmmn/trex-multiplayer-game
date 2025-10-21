using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace TRexServer
{
    public class DatabaseManager
    {
        private string connectionString;

        public DatabaseManager(string server, string database, string user, string password)
        {
            connectionString = $"Server={"localhost"};Database={"trex_game"};Uid={"root"};Pwd={"1234"};";
            InitializeDatabase();
        }

        // 데이터베이스 및 테이블 초기화
        private void InitializeDatabase()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // 플레이어 테이블 생성
                    string createPlayersTable = @"
                        CREATE TABLE IF NOT EXISTS players (
                            player_id INT AUTO_INCREMENT PRIMARY KEY,
                            player_name VARCHAR(50) UNIQUE NOT NULL,
                            total_games INT DEFAULT 0,
                            total_wins INT DEFAULT 0,
                            total_losses INT DEFAULT 0,
                            highest_score INT DEFAULT 0,
                            created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            last_played DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                        )";

                    // 게임 기록 테이블 생성
                    string createGamesTable = @"
                        CREATE TABLE IF NOT EXISTS game_records (
                            game_id INT AUTO_INCREMENT PRIMARY KEY,
                            player1_id INT NOT NULL,
                            player2_id INT NOT NULL,
                            player1_score INT NOT NULL,
                            player2_score INT NOT NULL,
                            winner_id INT NOT NULL,
                            game_duration INT,
                            played_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                            FOREIGN KEY (player1_id) REFERENCES players(player_id),
                            FOREIGN KEY (player2_id) REFERENCES players(player_id),
                            FOREIGN KEY (winner_id) REFERENCES players(player_id)
                        )";

                    using (MySqlCommand cmd = new MySqlCommand(createPlayersTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    using (MySqlCommand cmd = new MySqlCommand(createGamesTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    Console.WriteLine("[DB] 데이터베이스 초기화 완료");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 초기화 실패: {ex.Message}");
            }
        }

        // 플레이어 등록 또는 가져오기
        public int GetOrCreatePlayer(string playerName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // 기존 플레이어 확인
                    string selectQuery = "SELECT player_id FROM players WHERE player_name = @name";
                    using (MySqlCommand cmd = new MySqlCommand(selectQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", playerName);
                        object result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            return Convert.ToInt32(result);
                        }
                    }

                    // 새 플레이어 생성
                    string insertQuery = "INSERT INTO players (player_name) VALUES (@name); SELECT LAST_INSERT_ID();";
                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", playerName);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 플레이어 생성 실패: {ex.Message}");
                return -1;
            }
        }

        // 게임 결과 저장
        public void SaveGameResult(int player1Id, int player2Id, int player1Score, int player2Score, int gameDuration)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    int winnerId = player1Score > player2Score ? player1Id : player2Id;

                    // 게임 기록 저장
                    string insertGame = @"
                        INSERT INTO game_records 
                        (player1_id, player2_id, player1_score, player2_score, winner_id, game_duration) 
                        VALUES (@p1, @p2, @s1, @s2, @winner, @duration)";

                    using (MySqlCommand cmd = new MySqlCommand(insertGame, conn))
                    {
                        cmd.Parameters.AddWithValue("@p1", player1Id);
                        cmd.Parameters.AddWithValue("@p2", player2Id);
                        cmd.Parameters.AddWithValue("@s1", player1Score);
                        cmd.Parameters.AddWithValue("@s2", player2Score);
                        cmd.Parameters.AddWithValue("@winner", winnerId);
                        cmd.Parameters.AddWithValue("@duration", gameDuration);
                        cmd.ExecuteNonQuery();
                    }

                    // 플레이어1 통계 업데이트
                    UpdatePlayerStats(conn, player1Id, player1Score, player1Id == winnerId);

                    // 플레이어2 통계 업데이트
                    UpdatePlayerStats(conn, player2Id, player2Score, player2Id == winnerId);

                    Console.WriteLine($"[DB] 게임 결과 저장 완료 (승자: {winnerId})");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 게임 결과 저장 실패: {ex.Message}");
            }
        }

        private void UpdatePlayerStats(MySqlConnection conn, int playerId, int score, bool isWinner)
        {
            string updateQuery = @"
                UPDATE players 
                SET total_games = total_games + 1,
                    total_wins = total_wins + @win,
                    total_losses = total_losses + @loss,
                    highest_score = GREATEST(highest_score, @score)
                WHERE player_id = @id";

            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
            {
                cmd.Parameters.AddWithValue("@id", playerId);
                cmd.Parameters.AddWithValue("@win", isWinner ? 1 : 0);
                cmd.Parameters.AddWithValue("@loss", isWinner ? 0 : 1);
                cmd.Parameters.AddWithValue("@score", score);
                cmd.ExecuteNonQuery();
            }
        }

        // 플레이어 통계 조회
        public PlayerStats GetPlayerStats(string playerName)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT player_id, player_name, total_games, total_wins, total_losses, highest_score
                        FROM players 
                        WHERE player_name = @name";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", playerName);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new PlayerStats
                                {
                                    PlayerId = reader.GetInt32(0),
                                    PlayerName = reader.GetString(1),
                                    TotalGames = reader.GetInt32(2),
                                    TotalWins = reader.GetInt32(3),
                                    TotalLosses = reader.GetInt32(4),
                                    HighestScore = reader.GetInt32(5)
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 통계 조회 실패: {ex.Message}");
            }

            return null;
        }

        // 리더보드 조회 (상위 10명)
        public List<PlayerStats> GetLeaderboard(int limit = 10)
        {
            List<PlayerStats> leaderboard = new List<PlayerStats>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT player_id, player_name, total_games, total_wins, total_losses, highest_score
                        FROM players 
                        WHERE total_games > 0
                        ORDER BY total_wins DESC, highest_score DESC
                        LIMIT @limit";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@limit", limit);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                leaderboard.Add(new PlayerStats
                                {
                                    PlayerId = reader.GetInt32(0),
                                    PlayerName = reader.GetString(1),
                                    TotalGames = reader.GetInt32(2),
                                    TotalWins = reader.GetInt32(3),
                                    TotalLosses = reader.GetInt32(4),
                                    HighestScore = reader.GetInt32(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 리더보드 조회 실패: {ex.Message}");
            }

            return leaderboard;
        }

        // 최근 게임 기록 조회
        public List<GameRecord> GetRecentGames(int playerId, int limit = 5)
        {
            List<GameRecord> games = new List<GameRecord>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT g.game_id, g.player1_id, g.player2_id, 
                               g.player1_score, g.player2_score, g.winner_id,
                               p1.player_name as player1_name, p2.player_name as player2_name,
                               g.played_at
                        FROM game_records g
                        JOIN players p1 ON g.player1_id = p1.player_id
                        JOIN players p2 ON g.player2_id = p2.player_id
                        WHERE g.player1_id = @pid OR g.player2_id = @pid
                        ORDER BY g.played_at DESC
                        LIMIT @limit";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", playerId);
                        cmd.Parameters.AddWithValue("@limit", limit);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                games.Add(new GameRecord
                                {
                                    GameId = reader.GetInt32(0),
                                    Player1Id = reader.GetInt32(1),
                                    Player2Id = reader.GetInt32(2),
                                    Player1Score = reader.GetInt32(3),
                                    Player2Score = reader.GetInt32(4),
                                    WinnerId = reader.GetInt32(5),
                                    Player1Name = reader.GetString(6),
                                    Player2Name = reader.GetString(7),
                                    PlayedAt = reader.GetDateTime(8)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB 오류] 게임 기록 조회 실패: {ex.Message}");
            }

            return games;
        }
    }

    // 플레이어 통계 모델
    public class PlayerStats
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public int TotalGames { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public int HighestScore { get; set; }

        public double WinRate
        {
            get
            {
                if (TotalGames == 0) return 0;
                return (double)TotalWins / TotalGames * 100;
            }
        }

        public override string ToString()
        {
            return $"{PlayerName} - 전적: {TotalWins}승 {TotalLosses}패 (승률: {WinRate:F1}%) | 최고점수: {HighestScore}";
        }
    }

    // 게임 기록 모델
    public class GameRecord
    {
        public int GameId { get; set; }
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        public string Player1Name { get; set; }
        public string Player2Name { get; set; }
        public int Player1Score { get; set; }
        public int Player2Score { get; set; }
        public int WinnerId { get; set; }
        public DateTime PlayedAt { get; set; }

        public override string ToString()
        {
            string winner = WinnerId == Player1Id ? Player1Name : Player2Name;
            return $"[{PlayedAt:yyyy-MM-dd HH:mm}] {Player1Name}({Player1Score}) vs {Player2Name}({Player2Score}) - 승자: {winner}";
        }
    }
}