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

        private readonly Random m_random = new Random();
        // Form
        private TextBox m_resultTextBox;
        private Button m_balanceButton;
        private Button m_betButton;
        private PictureBox[][] m_columnPictures;
        private Image[] m_images;

        private Timer m_dropTimer;
        private Timer m_bounceTimer;
        private int m_currentStoppingColumn = 0;
        private bool m_isStopping = false;
        const int m_step = 50;
        private Point[][] m_originalPositions;
        private DateTime m_startTime;
        private double m_balance = 0;
        private double m_currentBet = 0.5;
        private bool[] m_reelStopped;

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
            InitializePositions();
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

            m_bounceTimer = new Timer();
            m_bounceTimer.Interval = 10;
            m_bounceTimer.Tick += BounceTimer_Tick;
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

        public void InitializePositions()
        {
            m_originalPositions = new Point[NUM_OF_REELS][];
            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                m_originalPositions[col] = new Point[PICTURES_PER_REEL];
                for (int r = 0; r < PICTURES_PER_REEL; r++)
                    m_originalPositions[col][r] = m_columnPictures[col][r].Location;
            }
        }

        private void StartBounceForColumn(int col)
        {
            for (int r = 0; r < PICTURES_PER_REEL; r++)
            {
                PictureBox pic = m_columnPictures[col][r];
                int targetY = m_originalPositions[col][r].Y;
                int dy = targetY - pic.Top;

                pic.Top += Math.Sign(dy) * 15;
                pic.Refresh();
                System.Threading.Thread.Sleep(20);
                pic.Top = targetY;
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
                    m_columnPictures[col][row].Tag = (Simboli)(m_randomIndex + 1);
                }
            }
        }

        // ~~ Events ~~ //

        private void PlayButton_Click(object sender, EventArgs e)
        {
            if (m_balance < m_currentBet)
            {
                MessageBox.Show("Can't spin not enough m_balance!");
                return;
            }

            m_balance -= m_currentBet;
            m_balanceButton.Text = $"BALANCE: ${m_balance}";
            //ShuffleImages();
            m_startTime = DateTime.Now;
            m_reelStopped = new bool[NUM_OF_REELS];
            m_isStopping = false;
            m_currentStoppingColumn = 0;
            m_dropTimer.Start();
        }

        private void DropTimer_Tick(object sender, EventArgs e)
        {
            for (int col = 0; col < NUM_OF_REELS; col++)
            {
                if (m_reelStopped[col]) continue;

                for (int r = 0; r < PICTURES_PER_REEL; r++)
                    m_columnPictures[col][r].Top += m_step;

                var panel = m_columnPictures[col][0].Parent as Panel;
                int labelHeight = panel.Controls.OfType<Label>().FirstOrDefault()?.Height ?? 0;

                var bottomPic = m_columnPictures[col].Last();
                if (bottomPic.Top >= panel.Height + labelHeight)
                {
                    PictureBox recycled = bottomPic;

                    for (int i = PICTURES_PER_REEL - 1; i > 0; i--)
                    {
                        m_columnPictures[col][i] = m_columnPictures[col][i - 1];
                    }

                    recycled.Top = m_columnPictures[col][1].Top - recycled.Height - 10;
                    int newIdx = m_random.Next(m_images.Length);
                    recycled.Image = (Image)m_images[newIdx].Clone();
                    recycled.Tag = (Simboli)(newIdx + 1);

                    m_columnPictures[col][0] = recycled;
                }
            }

            if (!m_isStopping && (DateTime.Now - m_startTime).TotalMilliseconds >= 2000)
            {
                m_isStopping = true;
                m_currentStoppingColumn = 0;
            }

            if (m_isStopping && m_currentStoppingColumn < NUM_OF_REELS)
            {
                double elapsed = (DateTime.Now - m_startTime).TotalMilliseconds;
                if (elapsed >= 2000 + m_currentStoppingColumn * 600)
                {
                    m_reelStopped[m_currentStoppingColumn] = true;
                    StartBounceForColumn(m_currentStoppingColumn);
                    m_currentStoppingColumn++;
                }
            }

            if (m_reelStopped.All(r => r))
            {
                m_dropTimer.Stop();
                CheckRowsForFirstThreeColumns_VisibleOnly();
            }
        }

        private void BounceTimer_Tick(object sender, EventArgs e) { }

        private void CheckRowsForFirstThreeColumns_VisibleOnly()
        {
            m_resultTextBox.Clear();

            int colsToCheck = 3;
            int neededVisibleRows = 3;

            var visiblePerColumn = new System.Collections.Generic.List<PictureBox[]>();

            for (int col = 0; col < colsToCheck; col++)
            {
                var panel = m_columnPictures[col][0].Parent as Panel;
                if (panel == null)
                {
                    visiblePerColumn.Add(new PictureBox[0]);
                    continue;
                }

                var lbl = panel.Controls.OfType<Label>().FirstOrDefault();
                int labelBottom = lbl != null ? lbl.Height : 0;

                Rectangle visibleRect = new Rectangle(0, labelBottom, panel.Width, panel.Height - labelBottom);

                var visiblePics = m_columnPictures[col]
                    .Where(pic => pic != null && pic.Bounds.IntersectsWith(visibleRect) && pic.Visible)
                    .OrderBy(pic => pic.Top)
                    .ToArray();

                visiblePerColumn.Add(visiblePics);
            }

            for (int rowIndex = 0; rowIndex < neededVisibleRows; rowIndex++)
            {
                bool allHaveRow = true;
                for (int c = 0; c < colsToCheck; c++)
                {
                    if (visiblePerColumn[c].Length <= rowIndex)
                    {
                        allHaveRow = false;
                        break;
                    }
                }
                if (!allHaveRow) continue;

                var t0 = visiblePerColumn[0][rowIndex].Tag as Simboli?;
                var t1 = visiblePerColumn[1][rowIndex].Tag as Simboli?;
                var t2 = visiblePerColumn[2][rowIndex].Tag as Simboli?;

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
    }
}