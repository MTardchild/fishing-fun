#nullable enable
namespace FishingFun
{
    using log4net.Appender;
    using log4net.Core;
    using log4net.Repository.Hierarchy;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Timers;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MainWindow : Window, IAppender
    {
        private System.Drawing.Point lastPoint = System.Drawing.Point.Empty;
        public ObservableCollection<LogEntry> LogEntries { get; set; }

        private IBobberFinder bobberFinder;
        private IPixelClassifier pixelClassifier;
        private IBiteWatcher biteWatcher;
        private ReticleDrawer reticleDrawer = new ReticleDrawer();

        private FishingBot? bot;
        private int strikeValue = 7; // this is the depth the bobber must go for the bite to be detected
        private bool setImageBackgroundColour = true;
        private Timer WindowSizeChangedTimer;
        private System.Threading.Thread? botThread;

        public MainWindow()
        {
            InitializeComponent();

            ((Logger)FishingBot.logger.Logger).AddAppender(this);

            DataContext = LogEntries = new ObservableCollection<LogEntry>();
            pixelClassifier = new PixelClassifier();
            pixelClassifier.SetConfiguration(WowProcess.IsWowClassic());
            bobberFinder = new SearchBobberFinder(pixelClassifier);

            var imageProvider = bobberFinder as IImageProvider;
            if (imageProvider != null)
            {
                imageProvider.BitmapEvent += ImageProvider_BitmapEvent;
            }

            biteWatcher = new PositionBiteWatcher(strikeValue);
            WindowSizeChangedTimer = new Timer { AutoReset = false, Interval = 100 };
            WindowSizeChangedTimer.Elapsed += SizeChangedTimer_Elapsed;
            CardGrid.SizeChanged += MainWindow_SizeChanged;
            Closing += (s, e) => botThread?.Abort();

            CastKeyChooser.KeyChanged += (s, e) =>
            {
                Settings.Focus();
                bot?.SetCastKey(CastKeyChooser.Key);
            };
            
            LureKeyChooser.KeyChanged += (s, e) =>
            {
                Settings.Focus();
                bot?.SetCastKey(LureKeyChooser.Key);
            };
            
            DestroyKeyChooser.KeyChanged += (s, e) =>
            {
                Settings.Focus();
                bot?.SetCastKey(DestroyKeyChooser.Key);
            };
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Reset the timer so it only fires 100ms after the user stop dragging the window.
            WindowSizeChangedTimer.Stop();
            WindowSizeChangedTimer.Start();
        }

        private void SizeChangedTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatch(() =>
            {
                flyingFishAnimation.AnimationWidth = (int)ActualWidth;
                flyingFishAnimation.AnimationHeight = (int)ActualHeight;
                LogGrid.Height = LogFlipper.ActualHeight;
                GraphGrid.Height = GraphFlipper.ActualHeight;
                GraphGrid.Visibility = Visibility.Visible;
                GraphFlipper.IsFlipped = true;
                LogFlipper.IsFlipped = true;
                GraphFlipper.IsFlipped = false;
                LogFlipper.IsFlipped = false;
            });
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => bot?.Stop();

        private void Settings_Click(object sender, RoutedEventArgs e) => new ColourConfiguration(this.pixelClassifier).Show();

        private void CastKey_Click(object sender, RoutedEventArgs e) => CastKeyChooser.Focus();
        
        private void LureKey_Click(object sender, RoutedEventArgs e) => LureKeyChooser.Focus();
        
        private void DestroyKey_Click(object sender, RoutedEventArgs e) => DestroyKeyChooser.Focus();

        private void FishingEventHandler(object sender, FishingEvent e)
        {
            Dispatch(() =>
            {
                switch (e.Action)
                {
                    case FishingAction.BobberMove:
                        if (!this.GraphFlipper.IsFlipped)
                        {
                            this.Chart.Add(e.Amplitude);
                        }
                        break;

                    case FishingAction.Loot:
                        this.flyingFishAnimation.Start();
                        this.LootingGrid.Visibility = Visibility.Visible;
                        break;

                    case FishingAction.Cast:
                        this.Chart.ClearChart();
                        this.LootingGrid.Visibility = Visibility.Collapsed;
                        this.flyingFishAnimation.Stop();
                        setImageBackgroundColour = true;
                        break;
                };
            });
        }

        public void DoAppend(LoggingEvent loggingEvent)
        {
            Dispatch(() =>
                LogEntries.Insert(0, new LogEntry()
                {
                    DateTime = DateTime.Now,
                    Message = loggingEvent.RenderedMessage
                })
            );
        }

        private void SetImageVisibility(Image imageForVisible, Image imageForCollapsed, bool state)
        {
            imageForVisible.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
            imageForCollapsed.Visibility = !state ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetButtonStates(bool isBotRunning)
        {
            Dispatch(() =>
            {
                this.Play.IsEnabled = isBotRunning;
                this.Stop.IsEnabled = !this.Play.IsEnabled;
                SetImageVisibility(this.PlayImage, this.PlayImage_Disabled, this.Play.IsEnabled);
                SetImageVisibility(this.StopImage, this.StopImage_Disabled, this.Stop.IsEnabled);
            });
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (bot == null)
            {
                WowProcess.PressKey(ConsoleKey.Spacebar);
                System.Threading.Thread.Sleep(1500);

                SetButtonStates(false);
                botThread = new System.Threading.Thread(new System.Threading.ThreadStart(this.BotThread));
                botThread.Start();

                // Hide cards after 10 minutes
                var timer = new Timer { Interval = 1000 * 60 * 10, AutoReset = false };
                timer.Elapsed += (s, ev) => this.Dispatch(() => this.LogFlipper.IsFlipped = this.GraphFlipper.IsFlipped = true);
                timer.Start();
            }
        }

        public void BotThread()
        {
            bot = new FishingBot(bobberFinder, this.biteWatcher, CastKeyChooser.Key, LureKeyChooser.Key, DestroyKeyChooser.Key);
            bot.FishingEventHandler += FishingEventHandler;
            bot.Start();
            bot = null;
            SetButtonStates(true);
        }

        private void ImageProvider_BitmapEvent(object sender, BobberBitmapEvent e)
        {
            Dispatch(() =>
            {
                SetBackgroundImageColour(e);
                reticleDrawer.Draw(e.Bitmap, e.Point);
                var bitmapImage = e.Bitmap.ToBitmapImage();
                e.Bitmap.Dispose();
                this.Screenshot.Source = bitmapImage;
            });
        }

        private void SetBackgroundImageColour(BobberBitmapEvent e)
        {
            if (this.setImageBackgroundColour)
            {
                this.setImageBackgroundColour = false;
                this.ImageBackground.Background = e.Bitmap.GetBackgroundColourBrush();
            }
        }

        private void Dispatch(Action action)
        {
            Application.Current?.Dispatcher.BeginInvoke((Action)(() => action()));
            Application.Current?.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { }));
        }
    }
}