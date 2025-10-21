using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using TRexShared;

namespace T_Rex_Endless_Runner
{
    public partial class Form1 : Form
    {
        // 기존 게임 변수
        bool jumping = false;
        int jumpSpeed;
        int force = 12;
        int score = 0;
        int obstacleSpeed = 10;
        Random rand = new Random();
        int position;
        bool isGameOver = false;

        int scoreTimer = 0;
        int scoreInterval = 5;

        List<PictureBox> clouds = new List<PictureBox>();
        int cloudSpeed = 2;
        int maxClouds = 4;

        // 네트워킹 변수
        private TcpClient client;
        private NetworkStream stream;
        private Thread receiveThread;
        private bool isConnected = false;
        private bool isMatchFound = false;

        // 플레이어 정보 추가
        private string myNickname = "";
        private bool isNicknameSet = false;

        // 상대방 정보
        private PictureBox opponentTrex;
        private int opponentScore = 0;

        // UI 라벨들
        private Label opponentScoreLabel;
        private Label centerMessageLabel;
        private Label connectionStatusLabel;

        // 서버 정보
        private const string SERVER_IP = "10.10.21.119";
        private const int SERVER_PORT = 5000;

        public Form1()
        {
            InitializeComponent();
            CreateClouds();
            CreateGameUI();

            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // 닉네임 입력 다이얼로그 표시
            ShowNicknameDialog();
        }

        #region 닉네임 입력

        private void ShowNicknameDialog()
        {
            Form nicknameForm = new Form();
            nicknameForm.Text = "닉네임 입력";
            nicknameForm.Width = 400;
            nicknameForm.Height = 200;
            nicknameForm.StartPosition = FormStartPosition.CenterScreen;
            nicknameForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            nicknameForm.MaximizeBox = false;
            nicknameForm.MinimizeBox = false;

            Label label = new Label();
            label.Text = "닉네임을 입력하세요 (2-10자):";
            label.Location = new Point(30, 30);
            label.AutoSize = true;
            label.Font = new Font("맑은 고딕", 10);

            TextBox textBox = new TextBox();
            textBox.Location = new Point(30, 60);
            textBox.Width = 320;
            textBox.Font = new Font("맑은 고딕", 12);
            textBox.MaxLength = 10;

            Button okButton = new Button();
            okButton.Text = "확인";
            okButton.Location = new Point(200, 100);
            okButton.Width = 70;
            okButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "취소";
            cancelButton.Location = new Point(280, 100);
            cancelButton.Width = 70;
            cancelButton.DialogResult = DialogResult.Cancel;

            nicknameForm.Controls.Add(label);
            nicknameForm.Controls.Add(textBox);
            nicknameForm.Controls.Add(okButton);
            nicknameForm.Controls.Add(cancelButton);

            nicknameForm.AcceptButton = okButton;

            // 엔터키로 확인
            textBox.KeyPress += (s, ev) =>
            {
                if (ev.KeyChar == (char)Keys.Enter)
                {
                    okButton.PerformClick();
                    ev.Handled = true;
                }
            };

            DialogResult result = nicknameForm.ShowDialog();

            if (result == DialogResult.OK)
            {
                string nickname = textBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(nickname))
                {
                    MessageBox.Show("닉네임을 입력해주세요!", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ShowNicknameDialog(); // 다시 표시
                    return;
                }

                if (nickname.Length < 2 || nickname.Length > 10)
                {
                    MessageBox.Show("닉네임은 2-10자 사이여야 합니다!", "오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    ShowNicknameDialog(); // 다시 표시
                    return;
                }

                myNickname = nickname;
                ConnectToServer();
            }
            else
            {
                // 취소 시 프로그램 종료
                Application.Exit();
            }
        }

        #endregion

        #region UI 생성

        private void CreateClouds()
        {
            for (int i = 0; i < maxClouds; i++)
            {
                PictureBox cloud = new PictureBox();
                cloud.Size = new Size(120, 60);
                cloud.SizeMode = PictureBoxSizeMode.StretchImage;
                cloud.BackColor = Color.Transparent;
                cloud.Tag = "cloud";

                int cloudType = rand.Next(3, 6);
                switch (cloudType)
                {
                    case 3:
                        cloud.Image = Properties.Resources.cloud3;
                        break;
                    case 4:
                        cloud.Image = Properties.Resources.cloud4;
                        break;
                    case 5:
                        cloud.Image = Properties.Resources.cloud5;
                        break;
                }

                cloud.Left = this.ClientSize.Width + rand.Next(100, 300) * i;
                cloud.Top = rand.Next(50, 150);
                clouds.Add(cloud);
                this.Controls.Add(cloud);
                cloud.SendToBack();
            }
        }

        private void CreateGameUI()
        {
            txtScore.Text = "점수: 0";
            txtScore.Font = new Font("맑은 고딕", 12, FontStyle.Bold);
            txtScore.BackColor = Color.Transparent;
            txtScore.ForeColor = Color.Black;

            // 상대방 공룡
            opponentTrex = new PictureBox();
            opponentTrex.Size = new Size(30, 32);
            opponentTrex.SizeMode = PictureBoxSizeMode.StretchImage;
            opponentTrex.BackColor = Color.Transparent;
            opponentTrex.Image = Properties.Resources.running;
            opponentTrex.Left = 50;
            opponentTrex.Top = 35;
            opponentTrex.Visible = false;
            this.Controls.Add(opponentTrex);
            opponentTrex.BringToFront();

            // 상대 점수 라벨
            opponentScoreLabel = new Label();
            opponentScoreLabel.Text = "상대 점수: 0";
            opponentScoreLabel.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
            opponentScoreLabel.BackColor = Color.Transparent;
            opponentScoreLabel.ForeColor = Color.Gray;
            opponentScoreLabel.AutoSize = true;
            opponentScoreLabel.Left = this.ClientSize.Width - 150;
            opponentScoreLabel.Top = 10;
            opponentScoreLabel.Visible = false;
            this.Controls.Add(opponentScoreLabel);
            opponentScoreLabel.BringToFront();

            // 중앙 메시지 라벨
            centerMessageLabel = new Label();
            centerMessageLabel.Text = "";
            centerMessageLabel.Font = new Font("맑은 고딕", 20, FontStyle.Bold);
            centerMessageLabel.BackColor = Color.Transparent;
            centerMessageLabel.ForeColor = Color.Black;
            centerMessageLabel.AutoSize = true;
            centerMessageLabel.TextAlign = ContentAlignment.MiddleCenter;
            centerMessageLabel.Visible = false;
            this.Controls.Add(centerMessageLabel);
            centerMessageLabel.BringToFront();

            // 연결 상태 라벨
            connectionStatusLabel = new Label();
            connectionStatusLabel.Text = "서버 연결 중...";
            connectionStatusLabel.Font = new Font("맑은 고딕", 16, FontStyle.Bold);
            connectionStatusLabel.BackColor = Color.Transparent;
            connectionStatusLabel.ForeColor = Color.DarkSlateGray;
            connectionStatusLabel.AutoSize = true;
            this.Controls.Add(connectionStatusLabel);
            connectionStatusLabel.BringToFront();

            CenterLabel(connectionStatusLabel);
        }

        private void CenterLabel(Label label)
        {
            label.Left = (this.ClientSize.Width - label.Width) / 2;
            label.Top = (this.ClientSize.Height - label.Height) / 2;
        }

        #endregion

        #region 네트워킹

        private void ConnectToServer()
        {
            try
            {
                UpdateConnectionStatus("서버 연결 중...");

                client = new TcpClient();
                client.Connect(SERVER_IP, SERVER_PORT);
                stream = client.GetStream();
                isConnected = true;

                Console.WriteLine("서버 연결 성공!");

                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                Thread.Sleep(100);

                // 닉네임 전송
                SendMessage(new NetworkMessage(MessageType.SetNickname, myNickname));
                Console.WriteLine($"닉네임 전송: {myNickname}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"연결 오류: {ex.Message}");

                MessageBox.Show($"서버 연결 실패: {ex.Message}\n오프라인 모드로 실행됩니다.",
                    "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                UpdateConnectionStatus("오프라인 모드");
                Thread.Sleep(1000);
                HideConnectionStatus();
                GameReset();
            }
        }

        private void ReceiveMessages()
        {
            byte[] buffer = new byte[4096];

            try
            {
                while (isConnected && client.Connected)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("연결 종료됨");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"받은 메시지: {message}");

                    NetworkMessage msg = NetworkMessage.Deserialize(message);

                    if (msg != null)
                    {
                        ProcessServerMessage(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"수신 오류: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private void ProcessServerMessage(NetworkMessage msg)
        {
            if (!this.IsHandleCreated)
            {
                Console.WriteLine("폼 핸들이 아직 생성되지 않음");
                return;
            }

            this.Invoke((MethodInvoker)delegate
            {
                try
                {
                    switch (msg.Type)
                    {
                        case MessageType.NicknameAccepted:
                            isNicknameSet = true;
                            Console.WriteLine("닉네임 승인됨!");
                            UpdateConnectionStatus("매칭 대기열 진입 중...");
                            SendMessage(new NetworkMessage(MessageType.JoinQueue));
                            break;

                        case MessageType.NicknameDuplicate:
                            MessageBox.Show("이미 사용 중인 닉네임입니다!\n다른 닉네임을 입력해주세요.",
                                "닉네임 중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            Disconnect();
                            ShowNicknameDialog();
                            break;

                        case MessageType.WaitingForMatch:
                            UpdateConnectionStatus($"상대를 찾는 중입니다!\n닉네임: {myNickname}");
                            break;

                        case MessageType.MatchFound:
                            isMatchFound = true;
                            UpdateConnectionStatus("상대를 찾았습니다!");
                            Thread.Sleep(1000);
                            break;

                        case MessageType.GameStart:
                            StartMultiplayerGame(msg.Data);
                            break;

                        case MessageType.OpponentPosition:
                            UpdateOpponentPosition(msg.Data);
                            break;

                        case MessageType.OpponentDied:
                            OpponentDied(msg.Data);
                            break;

                        case MessageType.ConnectionError:
                            MessageBox.Show(msg.Data, "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            GameOver();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"메시지 처리 오류: {ex.Message}");
                }
            });
        }

        private void SendMessage(NetworkMessage msg)
        {
            try
            {
                if (isConnected && client != null && client.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(msg.Serialize());
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"전송 오류: {ex.Message}");
                Disconnect();
            }
        }

        private void Disconnect()
        {
            isConnected = false;

            try
            {
                stream?.Close();
                client?.Close();
            }
            catch { }
        }

        private void UpdateConnectionStatus(string status)
        {
            if (connectionStatusLabel.InvokeRequired)
            {
                connectionStatusLabel.Invoke((MethodInvoker)delegate
                {
                    connectionStatusLabel.Text = status;
                    CenterLabel(connectionStatusLabel);
                    connectionStatusLabel.Visible = true;
                });
            }
            else
            {
                connectionStatusLabel.Text = status;
                CenterLabel(connectionStatusLabel);
                connectionStatusLabel.Visible = true;
            }
        }

        private void HideConnectionStatus()
        {
            if (connectionStatusLabel.InvokeRequired)
            {
                connectionStatusLabel.Invoke((MethodInvoker)delegate
                {
                    connectionStatusLabel.Visible = false;
                });
            }
            else
            {
                connectionStatusLabel.Visible = false;
            }
        }

        private void ShowCenterMessage(string message, Color color)
        {
            centerMessageLabel.Text = message;
            centerMessageLabel.ForeColor = color;
            CenterLabel(centerMessageLabel);
            centerMessageLabel.Visible = true;
        }

        private void HideCenterMessage()
        {
            centerMessageLabel.Visible = false;
        }

        #endregion

        #region 멀티플레이어 게임 로직

        private void StartMultiplayerGame(string mapData)
        {
            GameStartData gameStartData = GameStartData.Deserialize(mapData);
            ApplyObstacleMap(gameStartData);

            opponentTrex.Visible = true;
            opponentScoreLabel.Visible = true;

            HideConnectionStatus();
            GameReset();
        }

        private void ApplyObstacleMap(GameStartData gameData)
        {
            if (gameData == null || gameData.Obstacles == null)
                return;

            int obstacleIndex = 0;
            foreach (Control x in this.Controls)
            {
                if (x is PictureBox && (string)x.Tag == "obstacle")
                {
                    if (obstacleIndex < gameData.Obstacles.Length)
                    {
                        ObstacleData obstacleData = gameData.Obstacles[obstacleIndex];
                        x.Left = obstacleData.InitialPosition + obstacleData.RandomOffset + (x.Width * 10);
                        obstacleIndex++;
                    }
                }
            }
        }

        private void UpdateOpponentPosition(string data)
        {
            PlayerPositionData posData = PlayerPositionData.Deserialize(data);
            if (posData != null)
            {
                int scaledMovement = (int)((posData.Top - 367) * 0.08);
                opponentTrex.Top = 35 + scaledMovement;

                opponentScore = posData.Score;
                opponentScoreLabel.Text = "상대 점수: " + opponentScore;
            }
        }

        private void OpponentDied(string scoreData)
        {
            int opponentFinalScore = int.Parse(scoreData);

            gameTimer.Stop();
            isGameOver = true;

            opponentTrex.Image = Properties.Resources.dead;

            ShowCenterMessage($"승리! 🎉\n내 점수: {score}\n상대 점수: {opponentFinalScore}\n\nR 키를 눌러 재매칭", Color.DarkGreen);
        }

        #endregion

        #region 게임 타이머 및 로직

        private void MainGameTimerEvent(object sender, EventArgs e)
        {
            trex.Top += jumpSpeed;

            if (!isGameOver)
            {
                MoveClouds();
            }

            scoreTimer++;
            if (scoreTimer >= scoreInterval)
            {
                score++;
                scoreTimer = 0;
            }

            txtScore.Text = "점수: " + score;

            if (jumping == true && force < 0)
            {
                jumping = false;
            }

            if (jumping == true)
            {
                jumpSpeed = -12;
                force -= 1;
            }
            else
            {
                jumpSpeed = 12;
            }

            if (trex.Top > 366 && jumping == false)
            {
                force = 12;
                trex.Top = 367;
                jumpSpeed = 0;
            }

            if (isConnected && isMatchFound && !isGameOver)
            {
                SendPlayerPosition();
            }

            foreach (Control x in this.Controls)
            {
                if (x is PictureBox && (string)x.Tag == "obstacle")
                {
                    x.Left -= obstacleSpeed;

                    if (x.Left < -100)
                    {
                        x.Left = this.ClientSize.Width + rand.Next(200, 500) + (x.Width * 15);
                    }

                    if (trex.Bounds.IntersectsWith(x.Bounds))
                    {
                        GameOver();
                    }
                }
            }

            if (score > 100)
            {
                obstacleSpeed = 15;
                scoreInterval = 4;
                cloudSpeed = 3;
            }
            if (score > 300)
            {
                obstacleSpeed = 20;
                scoreInterval = 3;
                cloudSpeed = 4;
            }
            if (score > 500)
            {
                obstacleSpeed = 25;
                scoreInterval = 2;
                cloudSpeed = 5;
            }
        }

        private void SendPlayerPosition()
        {
            PlayerPositionData posData = new PlayerPositionData(trex.Top, score, jumping);
            NetworkMessage msg = new NetworkMessage(MessageType.PlayerPosition, posData.Serialize());
            SendMessage(msg);
        }

        private void GameOver()
        {
            if (isGameOver)
                return;

            gameTimer.Stop();
            trex.Image = Properties.Resources.dead;
            isGameOver = true;

            HideCenterMessage();

            if (isConnected && isMatchFound)
            {
                SendMessage(new NetworkMessage(MessageType.PlayerDied, score.ToString()));
                ShowCenterMessage($"패배... 💀\n내 점수: {score}\n상대 점수: {opponentScore}\n\nR 키를 눌러 재매칭", Color.DarkRed);
            }
            else
            {
                txtScore.Text = "점수: " + score + " | R 키를 눌러 재시작";
            }
        }

        private void MoveClouds()
        {
            foreach (PictureBox cloud in clouds)
            {
                cloud.Left -= cloudSpeed;

                if (cloud.Left < -cloud.Width)
                {
                    cloud.Left = this.ClientSize.Width + rand.Next(100, 400);
                    cloud.Top = rand.Next(50, 150);
                }
            }
        }

        #endregion

        #region 키보드 입력

        private void keyisdown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && jumping == false && !isGameOver)
            {
                jumping = true;
            }
        }

        private void keyisup(object sender, KeyEventArgs e)
        {
            if (jumping == true)
            {
                jumping = false;
            }

            if (e.KeyCode == Keys.R && isGameOver == true)
            {
                if (isConnected && isMatchFound)
                {
                    isMatchFound = false;
                    opponentTrex.Visible = false;
                    opponentScoreLabel.Visible = false;
                    HideCenterMessage();
                    UpdateConnectionStatus("재매칭 대기 중...");
                    SendMessage(new NetworkMessage(MessageType.RequestRematch));
                }
                else
                {
                    GameReset();
                }
            }
        }

        #endregion

        #region 게임 리셋

        private void GameReset()
        {
            force = 12;
            jumpSpeed = 0;
            jumping = false;
            score = 0;
            opponentScore = 0;
            obstacleSpeed = 10;
            scoreTimer = 0;
            scoreInterval = 5;
            cloudSpeed = 2;
            txtScore.Text = "점수: " + score;
            trex.Image = Properties.Resources.running;
            isGameOver = false;
            trex.Top = 367;

            HideCenterMessage();

            if (opponentTrex != null)
            {
                opponentTrex.Image = Properties.Resources.running;
                opponentTrex.Top = 35;
                opponentScoreLabel.Text = "상대 점수: 0";
            }

            foreach (Control x in this.Controls)
            {
                if (x is PictureBox && (string)x.Tag == "obstacle")
                {
                    position = this.ClientSize.Width + rand.Next(500, 800) + (x.Width * 10);
                    x.Left = position;
                }
            }

            for (int i = 0; i < clouds.Count; i++)
            {
                clouds[i].Left = this.ClientSize.Width + rand.Next(100, 300) * (i + 1);
                clouds[i].Top = rand.Next(50, 150);
            }

            gameTimer.Start();
        }

        #endregion

        #region 기타

        private void pictureBox2_Click(object sender, EventArgs e)
        {
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Disconnect();
            base.OnFormClosing(e);
        }

        #endregion
    }
}