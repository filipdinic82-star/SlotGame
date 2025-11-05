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
        private int animationStep = 0;
        const int Step = 50;
        private Point[][] originalPositions;
        private const int SpinDuzMs = 3000; 

        enum Simboli
        {
            A = 1,
            K = 2,
            J = 3,
            Dama = 4,
            Kralj = 5,
            Jack = 6,
        }

        public Form1()
        {
            InitializeComponent();

            this.Size = new Size(1080, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

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
                    {
                        pic.Location = new Point((columnWidth - pictureBoxSize) / 2, topMargin - pictureBoxSize - 10);
                    }
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
                {
                    originalPositions[col][r] = columnPictures[col][r].Location;
                }
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
                Font = new Font("Segoe UI", 16, FontStyle.Regular)
            };
            this.Controls.Add(resultTextBox);

            int buttonWidth = 140;
            int buttonHeight = 40;
            int buttonX = (this.ClientSize.Width - buttonWidth) / 2;
            int buttonY = this.ClientSize.Height - buttonHeight - 10;

            Button shuffleButton = new Button
            {
                Text = "PLAY",
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(buttonX, buttonY),
                BackColor = Color.LightGray,
                Font = new Font("Segoe UI", 14, FontStyle.Bold)
            };
            shuffleButton.Click += ShuffleButton_Click;
            this.Controls.Add(shuffleButton);

            dropTimer = new Timer();
            dropTimer.Interval = 25;
            dropTimer.Tick += dropTimer_Tick;

            ShuffleImages();
        }

        private DateTime spinStart;
        private void ShuffleButton_Click(object sender, EventArgs e)
        {
            animationStep = 0;
            spinStart = DateTime.Now;

            for (int col = 0; col < columnPictures.Length; col++)
            {
                int newIndex = random.Next(images.Length);
                var top = columnPictures[col][0];
                top.Image = (Image)images[newIndex].Clone();
                top.Tag = (Simboli)(newIndex + 1);
                top.Top = columnPictures[col][1].Top - top.Height - 10;
                top.Visible = true;
            }

            dropTimer.Start();
        }

        private void dropTimer_Tick(object sender, EventArgs e)
        {
            animationStep++;

            for (int col = 0; col < columnPictures.Length; col++)
            {
                for (int r = 0; r < columnPictures[col].Length; r++)
                {
                    var pic = columnPictures[col][r];
                    pic.Top += Step;
                }

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

            
            if ((DateTime.Now - spinStart).TotalMilliseconds >= SpinDuzMs)
            {
                dropTimer.Stop();

                for (int col = 0; col < columnPictures.Length; col++)
                {
                    
                    var panel = columnPictures[col][0].Parent as Panel;
                    var lbl = panel.Controls.OfType<Label>().FirstOrDefault();
                    int labelBottom = lbl != null ? lbl.Height : 0;
                    Rectangle visibleRect = new Rectangle(0, labelBottom, panel.Width, panel.Height - labelBottom);

                    var visiblePics = columnPictures[col]
                        .Where(p => p.Bounds.IntersectsWith(visibleRect) && p.Visible)
                        .OrderBy(p => p.Top)
                        .ToArray();

                    if (visiblePics.Length >= 3)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            var pic = visiblePics[i];
                            pic.Location = originalPositions[col][i + 1];
                        }
                    }
                }

                CheckRowsForFirstThreeColumns_VisibleOnly();
            }
        }

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
                        case Simboli.Dama: rewardMessage = "$8"; break;
                        case Simboli.Kralj: rewardMessage = "$10"; break;
                        case Simboli.Jack: rewardMessage = "$12"; break;
                    }
                    resultTextBox.AppendText($"YOU WON: {rewardMessage}\r\n");
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }
    }
}
