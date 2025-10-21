using System;

namespace TRexShared
{
    // 메시지 타입 열거형
    public enum MessageType
    {
        // 클라이언트 -> 서버
        SetNickname,        // 닉네임 설정 (추가!)
        JoinQueue,          // 매칭 대기열 진입
        PlayerPosition,     // 플레이어 위치 전송
        PlayerDied,         // 플레이어 사망
        RequestRematch,     // 재매칭 요청

        // 서버 -> 클라이언트
        NicknameAccepted,   // 닉네임 승인 (추가!)
        NicknameDuplicate,  // 닉네임 중복 (추가!)
        WaitingForMatch,    // 매칭 대기 중
        MatchFound,         // 매칭 완료
        GameStart,          // 게임 시작 (장애물 맵 포함)
        OpponentPosition,   // 상대방 위치 수신
        OpponentDied,       // 상대방 사망
        GameResult,         // 게임 결과 (승/패)
        ConnectionError     // 연결 오류
    }

    // 네트워크 메시지 기본 클래스
    [Serializable]
    public class NetworkMessage
    {
        public MessageType Type { get; set; }
        public string Data { get; set; }

        public NetworkMessage() { }

        public NetworkMessage(MessageType type, string data = "")
        {
            Type = type;
            Data = data;
        }

        // 메시지를 문자열로 직렬화
        public string Serialize()
        {
            return $"{(int)Type}|{Data}";
        }

        // 문자열에서 메시지로 역직렬화
        public static NetworkMessage Deserialize(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            string[] parts = message.Split('|');
            if (parts.Length < 1)
                return null;

            NetworkMessage msg = new NetworkMessage();
            msg.Type = (MessageType)int.Parse(parts[0]);
            msg.Data = parts.Length > 1 ? parts[1] : "";
            return msg;
        }
    }

    // 플레이어 위치 데이터
    [Serializable]
    public class PlayerPositionData
    {
        public int Top { get; set; }
        public int Score { get; set; }
        public bool IsJumping { get; set; }

        public PlayerPositionData() { }

        public PlayerPositionData(int top, int score, bool isJumping)
        {
            Top = top;
            Score = score;
            IsJumping = isJumping;
        }

        public string Serialize()
        {
            return $"{Top},{Score},{IsJumping}";
        }

        public static PlayerPositionData Deserialize(string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length < 3)
                return null;

            return new PlayerPositionData
            {
                Top = int.Parse(parts[0]),
                Score = int.Parse(parts[1]),
                IsJumping = bool.Parse(parts[2])
            };
        }
    }

    // 장애물 데이터
    [Serializable]
    public class ObstacleData
    {
        public int InitialPosition { get; set; }
        public int RandomOffset { get; set; }
        public int ObstacleIndex { get; set; }

        public ObstacleData() { }

        public ObstacleData(int position, int offset, int index)
        {
            InitialPosition = position;
            RandomOffset = offset;
            ObstacleIndex = index;
        }

        public string Serialize()
        {
            return $"{InitialPosition},{RandomOffset},{ObstacleIndex}";
        }

        public static ObstacleData Deserialize(string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length < 3)
                return null;

            return new ObstacleData
            {
                InitialPosition = int.Parse(parts[0]),
                RandomOffset = int.Parse(parts[1]),
                ObstacleIndex = int.Parse(parts[2])
            };
        }
    }

    // 게임 시작 데이터 (장애물 맵)
    [Serializable]
    public class GameStartData
    {
        public ObstacleData[] Obstacles { get; set; }

        public GameStartData() { }

        public GameStartData(ObstacleData[] obstacles)
        {
            Obstacles = obstacles;
        }

        public string Serialize()
        {
            if (Obstacles == null || Obstacles.Length == 0)
                return "";

            string[] serialized = new string[Obstacles.Length];
            for (int i = 0; i < Obstacles.Length; i++)
            {
                serialized[i] = Obstacles[i].Serialize();
            }
            return string.Join(";", serialized);
        }

        public static GameStartData Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data))
                return new GameStartData { Obstacles = new ObstacleData[0] };

            string[] parts = data.Split(';');
            ObstacleData[] obstacles = new ObstacleData[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                obstacles[i] = ObstacleData.Deserialize(parts[i]);
            }
            return new GameStartData { Obstacles = obstacles };
        }
    }

    // 게임 결과 데이터
    [Serializable]
    public class GameResultData
    {
        public bool IsWinner { get; set; }
        public int YourScore { get; set; }
        public int OpponentScore { get; set; }

        public GameResultData() { }

        public GameResultData(bool isWinner, int yourScore, int opponentScore)
        {
            IsWinner = isWinner;
            YourScore = yourScore;
            OpponentScore = opponentScore;
        }

        public string Serialize()
        {
            return $"{IsWinner},{YourScore},{OpponentScore}";
        }

        public static GameResultData Deserialize(string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length < 3)
                return null;

            return new GameResultData
            {
                IsWinner = bool.Parse(parts[0]),
                YourScore = int.Parse(parts[1]),
                OpponentScore = int.Parse(parts[2])
            };
        }
    }
}