using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace Snake
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SoundPlayer player = new SoundPlayer();
        private SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();
        
        #region goddamnUser32
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_CODE = 0x0000001;
        const uint SC_CLOSE = 0xF060;
        #endregion
        //snake
        const int SnakeSquareSize = 20;
        private SolidColorBrush snakeBodyBrush = Brushes.Green;
        private SolidColorBrush snakeHeadBrush = Brushes.YellowGreen;
        private List<Python> snakeParts = new List<Python>();
        public enum SnakeDirection { Left, Right, Up, Down };
        private SnakeDirection snakeDirection = SnakeDirection.Right;
        private int snakeLength;
        const int SnakeStartLength = 3;
        const int SnakeStartSpeed = 400;
        const int SnakeSpeedThreshold = 100;
        private List<string> SoundPath = new();
        // tick & rnd
        Random random = new();
        private System.Windows.Threading.DispatcherTimer gameTickTimer = new System.Windows.Threading.DispatcherTimer();
        //food
        private UIElement snakeFood = null;
        private SolidColorBrush foodBrush = Brushes.Red;
        //score
        private int currentScore = 0;
        const int MaxHighscoreListEntryCount = 5;


        public ObservableCollection<SnakeHighscore> HighscoreList { get; set; } = new ObservableCollection<SnakeHighscore>();
        public MainWindow()
        {
            InitializeComponent();
            gameTickTimer.Tick += GameTickTimer_Tick;
            LoadHighscoreList();
            
        }

        public void DrawGameArea()
        {
            bool DrawingBackgroundDone = false;
            int nextX = 0, nextY = 0;
            int rowCounter = 0;
            bool nextIsOdd = false;
            while (DrawingBackgroundDone == false)
            {
                Ellipse ellipse = new()
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = nextIsOdd ? Brushes.White : Brushes.Black
                };
                Rectangle rect = new()
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = nextIsOdd ? Brushes.Cornsilk : Brushes.DeepPink
                };
                UIElement[] ui = { rect, ellipse };
                GameArea.Children.Add(ui[1]);
                Canvas.SetTop(ui[1], nextY);
                Canvas.SetLeft(ui[1], nextX);

                nextIsOdd = !nextIsOdd;
                nextX += SnakeSquareSize;
                if (nextX >= GameArea.ActualWidth)
                {
                    nextX = 0;
                    nextY += SnakeSquareSize;
                    rowCounter++;
                    nextIsOdd = (rowCounter % 2 != 0);

                }
                if (nextY >= GameArea.ActualHeight)
                {
                    DrawingBackgroundDone = true;
                }
            }
        }
       
        public List<string> GetMSongs()
        {
            
            for (int i = 0; i < 2; i++)
            {
                SoundPath.Add((i) + ".wav");
            }
            return SoundPath;
        }
        public void RandomSound()
        {
            GetMSongs();
            player.SoundLocation = SoundPath[random.Next(0, 1)];
            PlaySound();
        }

        public void LoadSound(string path)
        {
            
            player.SoundLocation = path;
            player.Load();
        }
        public void PlaySound()
        {
            player.Play();
        }
        public void CloseSound()
        {
            player.Stop();
        }


        private void DrawSnake()
        {
            foreach (Python snakePart in snakeParts)
            {
                if (snakePart.UiElement == null)
                {
                    snakePart.UiElement = new Rectangle()
                    {
                        Width = SnakeSquareSize,
                        Height = SnakeSquareSize,
                        Fill = (snakePart.IsHead ? snakeHeadBrush : snakeBodyBrush)
                    };
                    GameArea.Children.Add(snakePart.UiElement);
                    Canvas.SetTop(snakePart.UiElement, snakePart.pos.Y);
                    Canvas.SetLeft(snakePart.UiElement, snakePart.pos.X);
                }
            }
        }
        private void MoveSnake()
        {

            // Remove the last part of the snake, in preparation of the new part added below  
            while (snakeParts.Count >= snakeLength)
            {
                GameArea.Children.Remove(snakeParts[0].UiElement);
                snakeParts.RemoveAt(0);
            }
            // Next up, we'll add a new element to the snake, which will be the (new) head  
            // Therefore, we mark all existing parts as non-head (body) elements and then  
            // we make sure that they use the body brush  
            foreach (Python snakePart in snakeParts)
            {
                (snakePart.UiElement as Rectangle).Fill = snakeBodyBrush;
                snakePart.IsHead = false;
            }

            // Determine in which direction to expand the snake, based on the current direction  
            Python snakeHead = snakeParts[snakeParts.Count - 1];
            double nextX = snakeHead.pos.X;
            double nextY = snakeHead.pos.Y;
            switch (snakeDirection)
            {
                case SnakeDirection.Left:
                    nextX -= SnakeSquareSize;
                    break;
                case SnakeDirection.Right:
                    nextX += SnakeSquareSize;
                    break;
                case SnakeDirection.Up:
                    nextY -= SnakeSquareSize;
                    break;
                case SnakeDirection.Down:
                    nextY += SnakeSquareSize;
                    break;
            }

            // Now add the new head part to our list of snake parts...  
            snakeParts.Add(new Python()
            {
                pos = new Point(nextX, nextY),
                IsHead = true
            });
            //... and then have it drawn!  
            DrawSnake();
            // We'll get to this later...  
            DoCollisionCheck();
        }
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            DrawGameArea();
            
        }
        private void GameTickTimer_Tick(object sender, EventArgs e)
        {
            MoveSnake();
        }
        private void StartNewGame()
        {
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Collapsed;
            bdrEndOfGame.Visibility = Visibility.Collapsed;
            RandomSound();
            foreach (Python snakeBodyPart in snakeParts)
            {
                if (snakeBodyPart.UiElement != null)
                    GameArea.Children.Remove(snakeBodyPart.UiElement);
            }



            if (snakeFood != null)
            {
                GameArea.Children.Remove(snakeFood);
            }


            currentScore = 0;
            snakeLength = SnakeStartLength;
            snakeDirection = SnakeDirection.Right;
            snakeParts.Add(new Python() { pos = new Point(SnakeSquareSize * 5, SnakeSquareSize * 5) });
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(SnakeStartSpeed);



            DrawSnake();
            DrawSnakeFood();
            UpdateGameStatus();
       
            gameTickTimer.IsEnabled = true;
        }
        private Point GetNextFoodPosition()
        {
            int maxX = (int)(GameArea.ActualWidth / SnakeSquareSize);
            int maxY = (int)(GameArea.ActualHeight / SnakeSquareSize);
            int foodX = random.Next(0, maxX) * SnakeSquareSize;
            int foodY = random.Next(0, maxY) * SnakeSquareSize;

            foreach (Python snakePart in snakeParts)
            {
                if ((snakePart.pos.X == foodX) && (snakePart.pos.Y == foodY))
                    return GetNextFoodPosition();
            }

            return new Point(foodX, foodY);
        }
        private void DrawSnakeFood()
        {

            Point foodPosition = GetNextFoodPosition();
            snakeFood = new Ellipse()
            {
                Width = SnakeSquareSize,
                Height = SnakeSquareSize,
                Fill = foodBrush
            };
            GameArea.Children.Add(snakeFood);
            Canvas.SetTop(snakeFood, foodPosition.Y);
            Canvas.SetLeft(snakeFood, foodPosition.X);
        }

        #region justLikeC++

        /*
         * error will occur
         *  possibly because it cant get handle
         */
        //protected override void OnSourceInitialized(EventArgs e)
        //{
        //    base.OnSourceInitialized(e);

        //    // sets handle
        //    IntPtr hwnd = new WindowInteropHelper(this).Handle;
        //    IntPtr hMenu = GetSystemMenu(hwnd, false);
        //    if (hMenu != IntPtr.Zero)
        //    {
        //        //will grey out the close button
        //        EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_CODE);
        //    }
        //}
        #endregion

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            SnakeDirection originalSnakeDirection = snakeDirection;
            switch (e.Key)
            {
                case Key.Up:
                    if (snakeDirection != SnakeDirection.Down)
                        snakeDirection = SnakeDirection.Up;
                    break;
                case Key.Down:
                    if (snakeDirection != SnakeDirection.Up)
                        snakeDirection = SnakeDirection.Down;
                    break;
                case Key.Left:
                    if (snakeDirection != SnakeDirection.Right)
                        snakeDirection = SnakeDirection.Left;
                    break;
                case Key.Right:
                    if (snakeDirection != SnakeDirection.Left)
                        snakeDirection = SnakeDirection.Right;
                    break;
                case Key.P:

                    //switch (paused)
                    //{
                    //    case false:
                    //        Pause();
                    //       bool paused = true;
                    //        break;
                    //    case true:
                    //        UnPause();
                    //        paused = false;
                    //        break;
                    //}
                    Pause();

                    break;
                case Key.O:
                    UnPause();
                    break;
                case Key.L:
                    snakeLength += 5;
                    break;
                case Key.K:
                    currentScore += 5;
                    break;
                case Key.Space:
                    StartNewGame();
                    break;
            }
            if (snakeDirection != originalSnakeDirection)
                MoveSnake();

        }

        private void DoCollisionCheck()
        {
            Python snakeHead = snakeParts[snakeParts.Count - 1];

            if ((snakeHead.pos.X == Canvas.GetLeft(snakeFood)) && (snakeHead.pos.Y == Canvas.GetTop(snakeFood)))
            {
                EatSnakeFood();
                return;
            }

            if ((snakeHead.pos.Y < 0) || (snakeHead.pos.Y >= GameArea.ActualHeight) ||
            (snakeHead.pos.X < 0) || (snakeHead.pos.X >= GameArea.ActualWidth))
            {
                EndGame();
            }

            foreach (Python snakeBodyPart in snakeParts.Take(snakeParts.Count - 1))
            {
                if ((snakeHead.pos.X == snakeBodyPart.pos.X) && (snakeHead.pos.Y == snakeBodyPart.pos.Y))
                    EndGame();
            }
        }
        private void EatSnakeFood()
        {
            
            
            snakeLength++;
            currentScore++;
            int timerInterval = Math.Max(SnakeSpeedThreshold, (int)gameTickTimer.Interval.TotalMilliseconds - (currentScore * 2));
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(timerInterval);
            GameArea.Children.Remove(snakeFood);
            DrawSnakeFood();
            UpdateGameStatus();
        }
        private void UpdateGameStatus()
        {
            this.Title = "SnakeWPF - Score: " + currentScore + " - Game speed: " + gameTickTimer.Interval.TotalMilliseconds;
            this.tbStatusScore.Text = currentScore.ToString();
            this.tbStatusSpeed.Text = gameTickTimer.Interval.TotalMilliseconds.ToString();
        }
        private void EndGame()
        {
            
            
            bool isNewHighscore = false;
            if (currentScore > 0)
            {
                int lowestHighscore = (this.HighscoreList.Count > 0 ? this.HighscoreList.Min(x => x.Score) : 0);
                if ((currentScore > lowestHighscore) || (this.HighscoreList.Count < MaxHighscoreListEntryCount))
                {
                    bdrNewHighscore.Visibility = Visibility.Visible;
                    txtPlayerName.Focus();
                    isNewHighscore = true;
                }
            }
            if (!isNewHighscore)
            {
                tbFinalScore.Text = currentScore.ToString();
                bdrEndOfGame.Visibility = Visibility.Visible;
            }
            gameTickTimer.IsEnabled = false;

        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {

        }
        private void LoadHighscoreList()
        {
            if (File.Exists("snake_highscorelist.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<SnakeHighscore>));
                using (Stream reader = new FileStream("snake_highscorelist.xml", FileMode.Open))
                {
                    List<SnakeHighscore> tempList = (List<SnakeHighscore>)serializer.Deserialize(reader);
                    this.HighscoreList.Clear();
                    foreach (var item in tempList.OrderByDescending(x => x.Score))
                        this.HighscoreList.Add(item);
                }
            }
        }
        


        private void BtnShowHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Visible;           
        }
        private void SaveHighscoreList()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<SnakeHighscore>));
            using (Stream writer = new FileStream("snake_highscorelist.xml", FileMode.Create))
            {
                serializer.Serialize(writer, this.HighscoreList);
            }
        }

        private void BtnAddToHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            int newIndex = 0;
            // Where should the new entry be inserted?
            if ((this.HighscoreList.Count > 0) && (currentScore < this.HighscoreList.Max(x => x.Score)))
            {
                SnakeHighscore justAbove = this.HighscoreList.OrderByDescending(x => x.Score).First(x => x.Score >= currentScore);
                if (justAbove != null)
                    newIndex = this.HighscoreList.IndexOf(justAbove) + 1;
            }
            // Create & insert the new entry
            this.HighscoreList.Insert(newIndex, new SnakeHighscore()
            {
                PlayerName = txtPlayerName.Text,
                Score = currentScore
            });
            // Make sure that the amount of entries does not exceed the maximum
            while (this.HighscoreList.Count > MaxHighscoreListEntryCount)
                this.HighscoreList.RemoveAt(MaxHighscoreListEntryCount);

            SaveHighscoreList();

            bdrNewHighscore.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Visible;
        }

        public void Pause() { gameTickTimer.Tick -= GameTickTimer_Tick; }
        public void UnPause() { gameTickTimer.Tick += GameTickTimer_Tick; }

        private void AddLength(object sender, RoutedEventArgs e) => snakeLength += 5;

        private void AddScore(object sender, RoutedEventArgs e) => currentScore += 5;
    }
    public class Python
    {
        public UIElement UiElement { get; set; }
        public Point pos { get; set; }
        public bool IsHead { get; set; }


    }

    public class SnakeHighscore
    {
        public string PlayerName { get; set; }

        public int Score { get; set; }
    }

}
