using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Email;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace LessPass
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public static MainPage Instance;

        #region Acrylic logic
        private SpriteVisual acrylicSprite;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (acrylicSprite != null)
                acrylicSprite.Size = e.NewSize.ToVector2();

            // Apparently the app bar button's width is 40
            // We have two buttons, plus a margin of 30 on each side
            // + 36 because my calculations suck
            // https://msdn.microsoft.com/en-us/library/windows/apps/xaml/dn481531.aspx?f=255&MSPPError=-2147217396
            const int MinusWidth = (40 * 2) + (30 * 2) + 36;

            LengthSlider.Width = e.NewSize.Width - MinusWidth;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;

            acrylicSprite = compositor.CreateSpriteVisual();

            ElementCompositionPreview.SetElementChildVisual(BackgroundGrid, acrylicSprite);

            acrylicSprite.Size = new Vector2((float)BackgroundGrid.ActualWidth, (float)BackgroundGrid.ActualHeight);
            acrylicSprite.Brush = compositor.CreateHostBackdropBrush();

            ApplicationViewTitleBar formattableTitleBar = ApplicationView.GetForCurrentView().TitleBar;

            formattableTitleBar.ButtonBackgroundColor = Colors.Transparent;
            formattableTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;

            coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(MainTitleBar);

            RevealChecked(RevealButton, null);
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Thickness padding = InputPanel.Padding;

            padding.Bottom = sender.Height;

            InputPanel.Padding = padding;

            RevealButton.Height = sender.Height;

            MainTitleBar.Height = sender.Height;
            MainTitleBar.Width  = CoreApplication.GetCurrentView().CoreWindow.Bounds.Width - sender.SystemOverlayLeftInset - sender.SystemOverlayRightInset;
            MainTitleBar.Margin = new Thickness(sender.SystemOverlayLeftInset, 0, sender.SystemOverlayRightInset, 0);
        }
        #endregion

        #region Dependency properties
        public string GeneratedPassword
        {
            get => (string)GetValue(GeneratedPasswordProperty);
            set => SetValue(GeneratedPasswordProperty, value);
        }

        public static readonly DependencyProperty GeneratedPasswordProperty =
            DependencyProperty.Register("GeneratedPassword", typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

        public int GeneratedPasswordLength
        {
            get => (int)GetValue(GeneratedPasswordLengthProperty);
            set => SetValue(GeneratedPasswordLengthProperty, value);
        }

        public static readonly DependencyProperty GeneratedPasswordLengthProperty =
            DependencyProperty.Register("GeneratedPasswordLength", typeof(int), typeof(MainPage), new PropertyMetadata(16, OnPropertyChanged));

        public bool EnableLowercase
        {
            get => (bool)GetValue(EnableLowercaseProperty);
            set => SetValue(EnableLowercaseProperty, value);
        }

        public static readonly DependencyProperty EnableLowercaseProperty =
            DependencyProperty.Register("EnableLowercase", typeof(bool), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool EnableUppercase
        {
            get => (bool)GetValue(EnableUppercaseProperty);
            set => SetValue(EnableUppercaseProperty, value);
        }

        public static readonly DependencyProperty EnableUppercaseProperty =
            DependencyProperty.Register("EnableUppercase", typeof(bool), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool EnableNumbers
        {
            get => (bool)GetValue(EnableNumbersProperty);
            set => SetValue(EnableNumbersProperty, value);
        }

        public static readonly DependencyProperty EnableNumbersProperty =
            DependencyProperty.Register("EnableNumbers", typeof(bool), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool EnableSymbols
        {
            get => (bool)GetValue(EnableSymbolsProperty);
            set => SetValue(EnableSymbolsProperty, value);
        }

        public static readonly DependencyProperty EnableSymbolsProperty =
            DependencyProperty.Register("EnableSymbols", typeof(bool), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public int Counter
        {
            get => (int)GetValue(CounterProperty);
            set => SetValue(CounterProperty, value);
        }

        public static readonly DependencyProperty CounterProperty =
            DependencyProperty.Register("Counter", typeof(int), typeof(MainPage), new PropertyMetadata(1, OnPropertyChanged));

        public int Iterations
        {
            get => (int)GetValue(IterationsProperty);
            set => SetValue(IterationsProperty, value);
        }

        public static readonly DependencyProperty IterationsProperty =
            DependencyProperty.Register("Iterations", typeof(int), typeof(MainPage), new PropertyMetadata(100_000, OnPropertyChanged));
        #endregion

        public bool IsValid => website.Length != 0 && username.Length != 0 && password.Length != 0;

        private Generator.Algorithms algorithm = Generator.Algorithms.Sha256;
        private string username = "";
        private string website = "";
        private string password = "";

        private Task<string> updateTask;
        private CancellationTokenSource cts;

        public MainPage()
        {
            InitializeComponent();

            Instance = this;

            ElementSoundPlayer.State = ElementSoundPlayerState.On;

            Application.Current.Resources["ToggleButtonBackgroundChecked"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPointerOver"] = new SolidColorBrush(Colors.Transparent);
            Application.Current.Resources["ToggleButtonBackgroundCheckedPressed"] = new SolidColorBrush(Colors.Transparent);
        }

        #region Event handlers
        private void OnInputChanged()
        {
            if (IsValid)
            {
                UpdateGeneratedPassword();
                CopyButton.IsEnabled = true;
            }
            else
            {
                GeneratedPassword = string.Empty;
                CopyButton.IsEnabled = false;
            }
        }

        private void UsernameChanged(object sender, TextChangedEventArgs e)
        {
            // ReSharper disable once PossibleNullReferenceException
            username = (sender as TextBox).Text;

            OnInputChanged();
        }

        private void WebsiteChanged(object sender, TextChangedEventArgs e)
        {
            // ReSharper disable once PossibleNullReferenceException
            website = (sender as TextBox).Text;

            OnInputChanged();
        }

        private void PasswordChanged(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once PossibleNullReferenceException
            password = (sender as PasswordBox).Password;

            OnInputChanged();
        }

        private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as MainPage)?.OnInputChanged();
        }

        private void UpdateGeneratedPassword()
        {
            if (cts != null && updateTask.Status != TaskStatus.RanToCompletion)
            {
                cts.Cancel();
                cts.Dispose();
            }

            cts = new CancellationTokenSource();

            string salt = string.Concat(website, username, Counter.ToString("X"));

            Generator.CharSets charSets = Generator.CharSets.None;

            if (EnableLowercase)
                charSets |= Generator.CharSets.Lowercase;
            if (EnableUppercase)
                charSets |= Generator.CharSets.Uppercase;
            if (EnableNumbers)
                charSets |= Generator.CharSets.Numbers;
            if (EnableSymbols)
                charSets |= Generator.CharSets.Symbols;

            if (charSets == Generator.CharSets.None)
            {
                ResourceLoader loader = ResourceLoader.GetForCurrentView();
                ContentDialog dialog = new ContentDialog
                {
                    Title = loader.GetString("CharsetError/Title"),
                    Content = loader.GetString("CharsetError/Content"),
                    IsPrimaryButtonEnabled = false,
                    IsSecondaryButtonEnabled = false,
                    
                    DefaultButton = ContentDialogButton.Close,
                    CloseButtonText = loader.GetString("CharsetError/Ok")
                };

                dialog.ShowAsync();
            }

            updateTask = Generator.GenerateAsync(password, salt, charSets,
                                                 digest: algorithm,
                                                 length: GeneratedPasswordLength,
                                                 iterations: (uint)Iterations,
                                                 cancellationToken: cts.Token);

            updateTask.ConfigureAwait(true).GetAwaiter().OnCompleted(() =>
                {
                    if (updateTask.Status == TaskStatus.RanToCompletion)
                        GeneratedPassword = updateTask.Result;
                });
        }
        #endregion

        private void GitHubClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://github.com/6A/LessPass"));
        }

        private void ContactClick(object sender, RoutedEventArgs e)
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView();
            EmailMessage emailMessage = new EmailMessage
            {
                Subject = loader.GetString("EmailSubject"),
                Body = loader.GetString("EmailBody")
            };

            emailMessage.To.Add(new EmailRecipient("s.aej+lesspass@outlook.com"));

            EmailManager.ShowComposeNewEmailAsync(emailMessage);
        }

        private void InconsolataClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://fonts.google.com/specimen/Inconsolata"));
        }

        private void NunitoClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://fonts.google.com/specimen/Nunito"));
        }

        private void InspiredByClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://lesspass.com"));
        }

        private void CopyClick(object sender, RoutedEventArgs e)
        {
            DataPackage data = new DataPackage();

            data.SetText(GeneratedPassword);

            Clipboard.SetContent(data);
        }

        private void AlgorithmChecked(object sender, RoutedEventArgs e)
        {
            algorithm = (Generator.Algorithms)int.Parse(((RadioButton)sender).Tag.ToString());

            OnInputChanged();
        }

        private void RevealChecked(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once PossibleInvalidOperationException
            bool isChecked = ((ToggleButton)sender).IsChecked.Value;

            ResultBlock.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            RevealIcon.Glyph = (isChecked ? '\uEE65' : '\uEC20').ToString();
        }

        public void SaveState(ApplicationDataContainer data)
        {
            data.Values[nameof(algorithm)] = (int)algorithm;
            data.Values[nameof(GeneratedPasswordLength)] = GeneratedPasswordLength;
            data.Values[nameof(Iterations)] = Iterations;
            data.Values[nameof(EnableLowercase)] = EnableLowercase;
            data.Values[nameof(EnableUppercase)] = EnableUppercase;
            data.Values[nameof(EnableNumbers)] = EnableNumbers;
            data.Values[nameof(EnableSymbols)] = EnableSymbols;

            data.Values[nameof(Counter)] = Counter;
            data.Values[nameof(RevealChecked)] = RevealButton.IsChecked;
        }

        public void LoadState(ApplicationDataContainer data)
        {
            if (data.Values.ContainsKey(nameof(algorithm)))
            {
                algorithm = (Generator.Algorithms)(int)data.Values[nameof(algorithm)];
                GeneratedPasswordLength = (int)data.Values[nameof(GeneratedPasswordLength)];
                Iterations = (int)data.Values[nameof(Iterations)];
                EnableLowercase = (bool)data.Values[nameof(EnableLowercase)];
                EnableUppercase = (bool)data.Values[nameof(EnableUppercase)];
                EnableNumbers = (bool)data.Values[nameof(EnableNumbers)];
                EnableSymbols = (bool)data.Values[nameof(EnableSymbols)];
                Counter = (int)data.Values[nameof(Counter)];

                RevealButton.IsChecked = (bool)data.Values[nameof(RevealChecked)];
            }
            else
            {
                algorithm = Generator.Algorithms.Sha256;
                GeneratedPasswordLength = 16;
                Iterations = 100_000;
                EnableLowercase = true;
                EnableUppercase = true;
                EnableNumbers = true;
                EnableSymbols = true;
                Counter = 1;

                RevealButton.IsChecked = true;
            }

            switch (algorithm)
            {
                case Generator.Algorithms.Sha256:
                    Sha256Radio.IsChecked = true;
                    break;

                case Generator.Algorithms.Sha384:
                    Sha384Radio.IsChecked = true;
                    break;

                case Generator.Algorithms.Sha512:
                    Sha512Radio.IsChecked = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
