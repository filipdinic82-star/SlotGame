using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Praksa
{
    public partial class Form1 : Form
    {
        // Constants
        private const int SYMBOL_WIDTH = 150;
        private const int SYMBOL_HEIGHT = 150;
        private const int REELS_GAP = 30;
        private const int SYMBOLS_GAP = 5;
        private const int NUM_OF_SYMBOLS = 6;

        private const int NUM_OF_REELS = 5;
        private const int SYMBOLS_PER_REEL = 3;
        private const int PICTURES_PER_REEL = SYMBOLS_PER_REEL + 1;

        private const int REEL1_DURATION = 1300;
        private const int REEL_STOPPAGE_DURATION = 300;
        private const int BOUNCE_OFFSET = 50; // px

        private readonly Random m_random = new Random();
        // Form
        private TextBox m_resultTextBox;
        private Button m_balanceButton;
        private Button m_betButton;
        private PictureBox[][] m_columnPictures;
        private Simboli[][] m_reelSymbols;
        private Image[] m_images;

        private Timer m_dropTimer;
        private int m_currentStoppingColumn = 0;
        const int m_step = 50;
        private DateTime m_startTime;
        private double m_balance = 0;
        private double m_currentBet = 0.5;

        // m_reelStopping for bounce
        private bool[] m_reelStopping;
        private bool[] m_reelStopped;
        private enum BounceState { None, Down, Up }
        private BounceState[] m_bounceState;

        enum Simboli
        {
            A = 1,
            K = 2,
            J = 3,
            Kruna = 4,
            Kovceg = 5,
            Sedmica = 6,
        }

        public Form1()
        {
            InitializeComponent();

            InitializeForm();
            LoadImages();
            InitializeReels();
            ShuffleImages();
        }

        public void InitializeForm()
        {
            // Form Window
            this.Size = new Size(1080, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Background
            string bgPath = Path.Combine(Application.StartupPath, "images", "pozadina.jpg");
            if (File.Exists(bgPath))
            {
                this.BackgroundImage = Image.FromFile(bgPath);
                this.BackgroundImageLayout = ImageLayout.Stretch;
            }

            // Controls
            m_resultTextBox = new TextBox
            {
                Name = "Dobitak",
                Multiline = false,
                ReadOnly = true,
                ScrollBars = ScrollBars.None,
                Size = new Size(this.ClientSize.Width - 580, 50),
                Location = new Point(300, 10),
                TextAlign = HorizontalAlignment.Center,
                BackColor = Color.Gray,
                Font = new Font("Segoe UI", 18, FontStyle.Bold)
            };
            this.Controls.Add(m_resultTextBox);

            Button playButton = new Button
            {
                Text = "PLAY",
                Size = new Size(140, 50),
                Location = new Point((this.ClientSize.Width - 140) / 2, this.ClientSize.Height - 60),
                BackColor = Color.LightGray,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            playButton.Click += PlayButton_Click;
            this.Controls.Add(playButton);

            m_balanceButton = new Button
            {
                Text = "BALANCE: $0",
                Size = new Size(190, 40),
                Location = new Point(260, this.ClientSize.Height - 50),
                BackColor = Color.LightGreen,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            m_balanceButton.Click += (s, e) =>
            {
                m_balance += 10;
                m_balanceButton.Text = $"BALANCE: ${m_balance}";
            };
            this.Controls.Add(m_balanceButton);

            m_betButton = new Button
            {
                Text = "BET: $0.5",
                Size = new Size(180, 40),
                Location = new Point(620, this.ClientSize.Height - 50),
                BackColor = Color.Orange,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            m_betButton.Click += (s, e) =>
            {
                if (m_currentBet == 0.5) m_currentBet = 1;
                else if (m_currentBet == 1) m_currentBet = 1.5;
                else if (m_currentBet == 1.5) m_currentBet = 2;
                else m_currentBet = 0.5;

                m_betButton.Text = $"BET: ${m_currentBet}";
            };
            this.Controls.Add(m_betButton);

            // Timers
            m_dropTimer = new Timer();
            m_dropTimer.Interval = 25;
            m_dropTimer.Tick += DropTimer_Tick;
        }

        public void LoadImages()
        {
            string imageFolder = Path.Combine(Application.StartupPath, "images");
            string[] imageFiles = Directory.GetFiles(imageFolder, "*.jpg").Take(NUM_OF_SYMBOLS).ToArray();

            m_images = new Image[NUM_OF_SYMBOLS];
            for (int i = 0; i < NUM_OF_SYMBOLS; i++)
            {
                byte[] bytes = File.ReadAllBytes(imageFiles[i]);
                using (var ms = new MemoryStream(bytes))
                {
                    m_images[i] = Image.FromStream(ms);
                }
            }
        }

        public void InitializeReels()
        {
            int layoutWidth = (SYMBOL_WIDTH * NUM_OF_REELS) + (REELS_GAP * (NUM_OF_REELS - 1));
            int columnHeight = SYMBOLS_PER_REEL * SYMBOL_HEIGHT + 2 * SYMBOLS_GAP;
            int startX = (this.ClientSize.Width - layoutWidth) / 2;
            int startY = (this.ClientSize.Height - columnHeight) / 2;

            m_columnPictures = new PictureBox[NUM_OF_REELS][];

            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                m_columnPictures[col] = new PictureBox[PICTURES_PER_REEL];

                int x = startX + col * (SYMBOL_WIDTH + REELS_GAP);
                Panel columnPanel = new Panel
                {
                    Size = new Size(SYMBOL_WIDTH, columnHeight),
                    Location = new Point(x, startY),
                    BackColor = Color.DarkGray,
                };

                for (int row = 0; row < PICTURES_PER_REEL; row++)
                {
                    PictureBox pic = new PictureBox
                    {
                        Size = new Size(SYMBOL_WIDTH, SYMBOL_HEIGHT),
                        BackColor = Color.WhiteSmoke,
                        BorderStyle = BorderStyle.FixedSingle,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };

                    if (row == 0)
                        pic.Location = new Point(0, -(SYMBOL_HEIGHT + SYMBOLS_GAP));
                    else
                    {
                        int visibleRowIndex = row - 1;
                        pic.Location = new Point(
                            0, visibleRowIndex * (SYMBOL_HEIGHT + SYMBOLS_GAP)
                        );
                    }

                    m_columnPictures[col][row] = pic;
                    columnPanel.Controls.Add(pic);
                }
                this.Controls.Add(columnPanel);
            }
        }

        private void ShuffleImages()
        {
            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                for (int row = 0; row < PICTURES_PER_REEL; row++)
                {
                    int m_randomIndex = m_random.Next(NUM_OF_SYMBOLS);
                    m_columnPictures[col][row].Image = (Image)m_images[m_randomIndex].Clone();
                    //m_reelSymbols[col][row] = (Simboli)(m_randomIndex + 1);
                    m_columnPictures[col][row].Tag = (Simboli)(m_randomIndex + 1);
                }
            }
        }

        private void CheckFowWins()
        {
            m_resultTextBox.Clear();

            for (int rowIndex = 0; rowIndex < SYMBOLS_PER_REEL; rowIndex++)
            {
                //var t0 = m_reelSymbols[0][rowIndex] as Simboli?;
                //var t1 = m_reelSymbols[1][rowIndex] as Simboli?;
                //var t2 = m_reelSymbols[2][rowIndex] as Simboli?;
                var t0 = m_columnPictures[0][rowIndex + 1].Tag as Simboli?;
                var t1 = m_columnPictures[1][rowIndex + 1].Tag as Simboli?;
                var t2 = m_columnPictures[2][rowIndex + 1].Tag as Simboli?;

                if (t0.HasValue && t1.HasValue && t2.HasValue && t0.Value == t1.Value && t1.Value == t2.Value)
                {
                    string rewardMessage = "$0";
                    switch (t0.Value)
                    {
                        case Simboli.A: rewardMessage = "$3"; break;
                        case Simboli.K: rewardMessage = "$4"; break;
                        case Simboli.J: rewardMessage = "$5"; break;
                        case Simboli.Kruna: rewardMessage = "$8"; break;
                        case Simboli.Kovceg: rewardMessage = "$10"; break;
                        case Simboli.Sedmica: rewardMessage = "$12"; break;
                    }
                    m_resultTextBox.AppendText($"YOU WON: {rewardMessage}\r\n");

                    double rewardAmount = double.Parse(rewardMessage.Replace("$", ""));
                    m_balance += rewardAmount;
                    m_balanceButton.Text = $"BALANCE: ${m_balance}";
                }
            }
        }

        // ~~ Events ~~ //

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (m_balance < m_currentBet)
            {
                MessageBox.Show("Can't spin not enough balance!");
                return;
            }

            m_balance -= m_currentBet;
            m_balanceButton.Text = $"BALANCE: ${m_balance}";
            //ShuffleImages();
            m_startTime = DateTime.Now;
            m_reelStopping = new bool[NUM_OF_REELS];
            m_reelStopped = new bool[NUM_OF_REELS];
            m_currentStoppingColumn = 0;
            m_dropTimer.Start();
            m_bounceState = new BounceState[NUM_OF_REELS];
        }

        private void DropTimer_Tick(object sender, EventArgs e)
        {
            // 1) NORMALNO SPINOVANJE — samo oni koji nisu stopirani i nisu u bounce-u
            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                if (m_reelStopped[col] || m_bounceState[col] != BounceState.None)
                    continue;

                for (int r = 0; r < PICTURES_PER_REEL; r++)
                    m_columnPictures[col][r].Top += m_step;

                var panel = m_columnPictures[col][0].Parent as Panel;

                var bottomPic = m_columnPictures[col].Last();
                if (bottomPic.Top >= panel.Height)
                {
                    PictureBox recycled = bottomPic;

                    for (int i = SYMBOLS_PER_REEL; i > 0; i--)
                        m_columnPictures[col][i] = m_columnPictures[col][i - 1];

                    recycled.Top = m_columnPictures[col][1].Top - SYMBOL_HEIGHT - SYMBOLS_GAP;
                    int newIdx = m_random.Next(NUM_OF_SYMBOLS);
                    recycled.Image = (Image)m_images[newIdx].Clone();
                    recycled.Tag = (Simboli)(newIdx + 1);

                    m_columnPictures[col][0] = recycled;
                }
            }

            // 2) VREME ZA ZAUSTAVLJANJE REELA
            double elapsed = (DateTime.Now - m_startTime).TotalMilliseconds;

            if (m_currentStoppingColumn < NUM_OF_REELS &&
                elapsed >= REEL1_DURATION + m_currentStoppingColumn * REEL_STOPPAGE_DURATION &&
                m_bounceState[m_currentStoppingColumn] == BounceState.None)
            {
                m_bounceState[m_currentStoppingColumn] = BounceState.Down;
            }

            // 3) ODRADA BOUNCE-A (DOWN → UP → STOP)
            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                if (m_bounceState[col] == BounceState.Down)
                {
                    // Spuštamo sve slike dok top prve slike ne dostigne offset
                    for (int r = 0; r < PICTURES_PER_REEL; r++)
                        m_columnPictures[col][r].Top += 25;

                    if (m_columnPictures[col][0].Top >= BOUNCE_OFFSET)
                    {
                        m_bounceState[col] = BounceState.Up;
                    }
                }
                else if (m_bounceState[col] == BounceState.Up)
                {
                    // Podigni slike dok slika index 1 ne ide na Top = 0
                    int currentTop = m_columnPictures[col][1].Top;

                    if (currentTop > 0)
                    {
                        int move = Math.Min(25, currentTop); // poslednji milimetar poravna precizno
                        for (int r = 0; r < PICTURES_PER_REEL; r++)
                            m_columnPictures[col][r].Top -= move;
                    }
                    else
                    {

                        m_reelStopped[col] = true;
                        m_bounceState[col] = BounceState.None;

                        m_currentStoppingColumn++;
                    }
                }
            }

            // Check when all reels are stopped
            if (m_reelStopped.All(r => r))
            {
                m_dropTimer.Stop();
                CheckFowWins();
            }
        }
    }
}