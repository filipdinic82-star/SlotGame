using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Praksa
{
    public partial class Form1 : Form
    {
        private readonly Random random = new Random();
        private PictureBox[][] columnPictures;
        private Image[] images;
        private Simboli[][] columnTypes;
        private TextBox resultTextBox;
        private Timer dropTimer;
        private Timer bounceTimer;
        private int currentStoppingColumn = 0;
        private bool isStopping = false;
        private bool isBouncing = false;
        private int animationStep = 0;
        const int Step = 50;
        private Point[][] originalPositions;
        private const int SpinDuzMs = 3000;
        private DateTime startTime;
        private double balance = 0;
        private double currentBet = 0.5;
        private Button balanceButton;
        private Button betButton;
        private bool[] reelStopped;

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

            this.Size = new Size(1080, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            string bgPath = Path.Combine(Application.StartupPath, "images", "pozadina.jpg");
            if (File.Exists(bgPath))
            {
                this.BackgroundImage = Image.FromFile(bgPath);
                this.BackgroundImageLayout = ImageLayout.Stretch;
            }

            int numColumns = 5;
            int columnWidth = 150;
            int columnHeight = 500;
            int spacing = 30;
            int pictureBoxSize = 150;
            int picturesPerColumn = 4;

            int layoutWidth = (columnWidth * numColumns) + (spacing * (numColumns - 1));
            int startX = (this.ClientSize.Width - layoutWidth) / 2;
            int startY = (this.ClientSize.Height - columnHeight) / 2;

            string imageFolder = Path.Combine(Application.StartupPath, "images");
            string[] imageFiles = Directory.GetFiles(imageFolder, "*.jpg").Take(6).ToArray();

            images = new Image[imageFiles.Length];
            for (int i = 0; i < imageFiles.Length; i++)
            {
                byte[] bytes = File.ReadAllBytes(imageFiles[i]);
                using (var ms = new MemoryStream(bytes))
                {
                    images[i] = Image.FromStream(ms);
                }
            }

            columnPictures = new PictureBox[numColumns][];
            columnTypes = new Simboli[numColumns][];

            for (int col = 0; col < numColumns; col++)
            {
                columnPictures[col] = new PictureBox[picturesPerColumn];
                columnTypes[col] = new Simboli[picturesPerColumn];

                int x = startX + col * (columnWidth + spacing);
                Panel column = new Panel
                {
                    Size = new Size(columnWidth, columnHeight),
                    Location = new Point(x, startY),
                    BackColor = Color.DarkGray,
                };

                Label lbl = new Label
                {
                    Text = $"Kolona {col + 1}",
                    Dock = DockStyle.Top,
                    Height = 25,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                column.Controls.Add(lbl);

                int visibleRows = picturesPerColumn - 1;
                int totalVisibleHeight = visibleRows * pictureBoxSize;
                int availableSpace = columnHeight - lbl.Height;
                int topMargin = lbl.Height + (availableSpace - totalVisibleHeight) / 2;
                int spaceBetween = 5;

                for (int row = 0; row < picturesPerColumn; row++)
                {
                    PictureBox pic = new PictureBox
                    {
                        Size = new Size(pictureBoxSize, pictureBoxSize),
                        BackColor = Color.WhiteSmoke,
                        BorderStyle = BorderStyle.FixedSingle,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };

                    if (row == 0)
                        pic.Location = new Point((columnWidth - pictureBoxSize) / 2, topMargin - pictureBoxSize - 10);
                    else
                    {
                        int visibleRowIndex = row - 1;
                        pic.Location = new Point(
                            (columnWidth - pictureBoxSize) / 2,
                            topMargin + visibleRowIndex * (pictureBoxSize + spaceBetween)
                        );
                    }

                    columnPictures[col][row] = pic;
                    column.Controls.Add(pic);
                }

                this.Controls.Add(column);
            }

            originalPositions = new Point[columnPictures.Length][];
            for (int col = 0; col < columnPictures.Length; col++)
            {
                originalPositions[col] = new Point[columnPictures[col].Length];
                for (int r = 0; r < columnPictures[col].Length; r++)
                    originalPositions[col][r] = columnPictures[col][r].Location;
            }

            resultTextBox = new TextBox
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
            this.Controls.Add(resultTextBox);

            Button shuffleButton = new Button
            {
                Text = "PLAY",
                Size = new Size(140, 50),
                Location = new Point((this.ClientSize.Width - 140) / 2, this.ClientSize.Height - 60),
                BackColor = Color.LightGray,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            shuffleButton.Click += ShuffleButton_Click;
            this.Controls.Add(shuffleButton);

            dropTimer = new Timer();
            dropTimer.Interval = 25;
            dropTimer.Tick += DropTimer_Tick;

            bounceTimer = new Timer();
            bounceTimer.Interval = 10;
            bounceTimer.Tick += BounceTimer_Tick;

            ShuffleImages();
            balanceButton = new Button
            {
                Text = "BALANCE: $0",
                Size = new Size(190, 40),
                Location = new Point(260, this.ClientSize.Height - 50),
                BackColor = Color.LightGreen,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            balanceButton.Click += (s, e) =>
            {
                balance += 10;
                balanceButton.Text = $"BALANCE: ${balance}";
            };
            this.Controls.Add(balanceButton);

            betButton = new Button
            {
                Text = "BET: $0.5",
                Size = new Size(180, 40),
                Location = new Point(620, this.ClientSize.Height - 50),
                BackColor = Color.Orange,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            betButton.Click += (s, e) =>
            {
                if (currentBet == 0.5) currentBet = 1;
                else if (currentBet == 1) currentBet = 1.5;
                else if (currentBet == 1.5) currentBet = 2;
                else currentBet = 0.5;

                betButton.Text = $"BET: ${currentBet}";
            };
            this.Controls.Add(betButton);
        }

        private void ShuffleButton_Click(object sender, EventArgs e)
        {
            if (balance < currentBet)
            {
                MessageBox.Show("Can't spin not enough balance!");
                return;
            }

            balance -= currentBet;
            balanceButton.Text = $"BALANCE: ${balance}";
            ShuffleImages();
            startTime = DateTime.Now;
            reelStopped = new bool[columnPictures.Length];
            isStopping = false;
            isBouncing = false;
            currentStoppingColumn = 0;
            dropTimer.Start();
        }

        private void DropTimer_Tick(object sender, EventArgs e)
        {
            for (int col = 0; col < columnPictures.Length; col++)
            {
                if (reelStopped[col]) continue;

                for (int r = 0; r < columnPictures[col].Length; r++)
                    columnPictures[col][r].Top += Step;

                var panel = columnPictures[col][0].Parent as Panel;
                int labelHeight = panel.Controls.OfType<Label>().FirstOrDefault()?.Height ?? 0;

                var bottomPic = columnPictures[col].Last();
                if (bottomPic.Top >= panel.Height + labelHeight)
                {
                    PictureBox recycled = bottomPic;

                    for (int i = columnPictures[col].Length - 1; i > 0; i--)
                    {
                        columnPictures[col][i] = columnPictures[col][i - 1];
                        columnTypes[col][i] = columnTypes[col][i - 1];
                    }

                    recycled.Top = columnPictures[col][1].Top - recycled.Height - 10;
                    int newIdx = random.Next(images.Length);
                    recycled.Image = (Image)images[newIdx].Clone();
                    recycled.Tag = (Simboli)(newIdx + 1);

                    columnPictures[col][0] = recycled;
                    columnTypes[col][0] = (Simboli)(newIdx + 1);
                }
            }

            if (!isStopping && (DateTime.Now - startTime).TotalMilliseconds >= 2000)
            {
                isStopping = true;
                currentStoppingColumn = 0;
            }

            if (isStopping && currentStoppingColumn < columnPictures.Length)
            {
                double elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (elapsed >= 2000 + currentStoppingColumn * 600)
                {
                    reelStopped[currentStoppingColumn] = true;
                    StartBounceForColumn(currentStoppingColumn);
                    currentStoppingColumn++;
                }
            }

            if (reelStopped.All(r => r))
            {
                dropTimer.Stop();
                CheckRowsForFirstThreeColumns_VisibleOnly();
            }
        }

        private void StartBounceForColumn(int col)
        {
            isBouncing = true;

            for (int r = 0; r < columnPictures[col].Length; r++)
            {
                PictureBox pic = columnPictures[col][r];
                int targetY = originalPositions[col][r].Y;
                int dy = targetY - pic.Top;

                pic.Top += Math.Sign(dy) * 15;
                pic.Refresh();
                System.Threading.Thread.Sleep(20);
                pic.Top = targetY;
            }

            isBouncing = false;
        }

        private void BounceTimer_Tick(object sender, EventArgs e) { }

        private void ShuffleImages()
        {
            for (int col = 0; col < columnPictures.Length; col++)
            {
                for (int row = 0; row < columnPictures[col].Length; row++)
                {
                    int randomIndex = random.Next(images.Length);
                    columnPictures[col][row].Image = (Image)images[randomIndex].Clone();
                    columnPictures[col][row].Tag = (Simboli)(randomIndex + 1);
                    columnTypes[col][row] = (Simboli)(randomIndex + 1);
                }
            }
        }

        private void CheckRowsForFirstThreeColumns_VisibleOnly()
        {
            resultTextBox.Clear();

            int colsToCheck = 3;
            int neededVisibleRows = 3;

            var visiblePerColumn = new System.Collections.Generic.List<PictureBox[]>();

            for (int col = 0; col < colsToCheck; col++)
            {
                var panel = columnPictures[col][0].Parent as Panel;
                if (panel == null)
                {
                    visiblePerColumn.Add(new PictureBox[0]);
                    continue;
                }

                var lbl = panel.Controls.OfType<Label>().FirstOrDefault();
                int labelBottom = lbl != null ? lbl.Height : 0;

                Rectangle visibleRect = new Rectangle(0, labelBottom, panel.Width, panel.Height - labelBottom);

                var visiblePics = columnPictures[col]
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
                    resultTextBox.AppendText($"YOU WON: {rewardMessage}\r\n");

                    double rewardAmount = double.Parse(rewardMessage.Replace("$", ""));
                    balance += rewardAmount;
                    balanceButton.Text = $"BALANCE: ${balance}";
                }
            }
        }
    }
}