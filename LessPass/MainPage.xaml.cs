using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Email;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace LessPass
{
    /// <summary>
    ///   The main (and only) page of the application.
    /// </summary>
    [SuppressMessage("Compiler", "CS4014")]
    public sealed partial class MainPage
    {
        public static MainPage Instance;
        
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //// Apparently the app bar button's width is 40
            //// We have two buttons, plus a margin of 30 on each side
            //// https://msdn.microsoft.com/en-us/library/windows/apps/xaml/dn481531.aspx?f=255&MSPPError=-2147217396
            const int minusWidth = (40 * 2) + (30 * 2);

            LengthSlider.Width = e.NewSize.Width - minusWidth;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplicationViewTitleBar formattableTitleBar = ApplicationView.GetForCurrentView().TitleBar;

            formattableTitleBar.ButtonBackgroundColor = Colors.Transparent;
            formattableTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            formattableTitleBar.ButtonForegroundColor = Colors.Black;
            formattableTitleBar.ButtonInactiveForegroundColor = Colors.Black;
            formattableTitleBar.ButtonHoverForegroundColor = Colors.White;
            formattableTitleBar.ButtonPressedForegroundColor = Colors.White;

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;

            coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(MainTitleBar);

            WebsiteBox.Focus(FocusState.Programmatic);
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Thickness padding = InputPanel.Padding;

            padding.Bottom = sender.Height;

            InputPanel.Padding = padding;

            RevealButton.Height = sender.Height;

            MainTitleBar.Height = sender.Height;
            MainTitleBar.Width = CoreApplication.GetCurrentView().CoreWindow.Bounds.Width - sender.SystemOverlayLeftInset - sender.SystemOverlayRightInset;
            MainTitleBar.Margin = new Thickness(sender.SystemOverlayLeftInset, 0, sender.SystemOverlayRightInset, 0);
        }

        #region Dependency properties
        public string GeneratedPassword
        {
            get => (string)GetValue(GeneratedPasswordProperty);
            set => SetValue(GeneratedPasswordProperty, value);
        }

        public static readonly DependencyProperty GeneratedPasswordProperty =
            DependencyProperty.Register("GeneratedPassword", typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

        public double GeneratedPasswordLength
        {
            get => (double)GetValue(GeneratedPasswordLengthProperty);
            set => SetValue(GeneratedPasswordLengthProperty, value);
        }

        public static readonly DependencyProperty GeneratedPasswordLengthProperty =
            DependencyProperty.Register("GeneratedPasswordLength", typeof(double), typeof(MainPage), new PropertyMetadata(16.0, OnPropertyChanged));

        public bool? EnableLowercase
        {
            get => (bool?)GetValue(EnableLowercaseProperty);
            set => SetValue(EnableLowercaseProperty, value);
        }

        public static readonly DependencyProperty EnableLowercaseProperty =
            DependencyProperty.Register("EnableLowercase", typeof(bool?), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool? EnableUppercase
        {
            get => (bool?)GetValue(EnableUppercaseProperty);
            set => SetValue(EnableUppercaseProperty, value);
        }

        public static readonly DependencyProperty EnableUppercaseProperty =
            DependencyProperty.Register("EnableUppercase", typeof(bool?), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool? EnableNumbers
        {
            get => (bool?)GetValue(EnableNumbersProperty);
            set => SetValue(EnableNumbersProperty, value);
        }

        public static readonly DependencyProperty EnableNumbersProperty =
            DependencyProperty.Register("EnableNumbers", typeof(bool?), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public bool? EnableSymbols
        {
            get => (bool?)GetValue(EnableSymbolsProperty);
            set => SetValue(EnableSymbolsProperty, value);
        }

        public static readonly DependencyProperty EnableSymbolsProperty =
            DependencyProperty.Register("EnableSymbols", typeof(bool?), typeof(MainPage), new PropertyMetadata(true, OnPropertyChanged));

        public double Counter
        {
            get => (double)GetValue(CounterProperty);
            set => SetValue(CounterProperty, value);
        }

        public static readonly DependencyProperty CounterProperty =
            DependencyProperty.Register("Counter", typeof(double), typeof(MainPage), new PropertyMetadata(1.0, OnPropertyChanged));

        public double Iterations
        {
            get => (double)GetValue(IterationsProperty);
            set => SetValue(IterationsProperty, value);
        }

        public static readonly DependencyProperty IterationsProperty =
            DependencyProperty.Register("Iterations", typeof(double), typeof(MainPage), new PropertyMetadata(100_000.0, OnPropertyChanged));
        #endregion

        public bool IsValid => website.Length != 0 && username.Length != 0 && password.Length != 0;

        private Generator.Algorithms algorithm = Generator.Algorithms.Sha256;
        private bool isRevealChecked;
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

        private void AlgorithmChecked(object sender, RoutedEventArgs e)
        {
            algorithm = (Generator.Algorithms)int.Parse(((RadioButton)sender).Tag.ToString());

            OnInputChanged();
        }

        private void RevealChecked(object sender, RoutedEventArgs e)
        {
            // ReSharper disable once PossibleInvalidOperationException
            isRevealChecked = !isRevealChecked;

            void TurnOffVisibility(object _, object __)
            {
                ResultBlock.Visibility = Visibility.Collapsed;
                FadeOutStoryboard.Completed -= TurnOffVisibility;
            }

            if (isRevealChecked)
            {
                ResultBlock.Visibility = Visibility.Visible;
                FadeInStoryboard.Begin();
            }
            else
            {
                FadeOutStoryboard.Begin();
                FadeOutStoryboard.Completed += TurnOffVisibility;
            }
            
            RevealIcon.Glyph = (isRevealChecked ? '\uEE65' : '\uEC20').ToString();
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

            if (isRevealChecked)
                FadeOutStoryboard.Begin();

            cts = new CancellationTokenSource();

            string salt = string.Concat(website, username, ((int)Counter).ToString("X"));

            Generator.CharSets charSets = Generator.CharSets.None;

            if (EnableLowercase.Value)
                charSets |= Generator.CharSets.Lowercase;
            if (EnableUppercase.Value)
                charSets |= Generator.CharSets.Uppercase;
            if (EnableNumbers.Value)
                charSets |= Generator.CharSets.Numbers;
            if (EnableSymbols.Value)
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
                return;
            }

            updateTask = Generator.GenerateAsync(password, salt, charSets,
                                                 digest: algorithm,
                                                 length: (int)GeneratedPasswordLength,
                                                 iterations: (uint)Iterations,
                                                 cancellationToken: cts.Token);

            updateTask.ConfigureAwait(true).GetAwaiter().OnCompleted(() =>
            {
                if (updateTask.Status != TaskStatus.RanToCompletion)
                    return;

                GeneratedPassword = updateTask.Result;

                if (isRevealChecked)
                    FadeInStoryboard.Begin();
            });
        }
        #endregion

        private void GitHubClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://github.com/6A/LessPass"));
        }

        private void PrivacyClick(object sender, RoutedEventArgs e)
        {
            Launcher.LaunchUriAsync(new Uri("https://github.com/6A/LessPass/blob/master/PRIVACY.md"));
        }

        private void ContactClick(object sender, RoutedEventArgs e)
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView();
            EmailMessage emailMessage = new EmailMessage
            {
                Subject = loader.GetString("EmailSubject"),
                Body = loader.GetString("EmailBody")
            };

            emailMessage.To.Add(new EmailRecipient("support+lesspass@gregoirege.is"));

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
            data.Values[nameof(RevealChecked)] = isRevealChecked;
        }

        public void LoadState(ApplicationDataContainer data)
        {
            if (data.Values.ContainsKey(nameof(algorithm)))
            {
                algorithm = (Generator.Algorithms)(int)data.Values[nameof(algorithm)];
                GeneratedPasswordLength = (double)data.Values[nameof(GeneratedPasswordLength)];
                Iterations = (double)data.Values[nameof(Iterations)];
                EnableLowercase = (bool?)data.Values[nameof(EnableLowercase)];
                EnableUppercase = (bool?)data.Values[nameof(EnableUppercase)];
                EnableNumbers = (bool?)data.Values[nameof(EnableNumbers)];
                EnableSymbols = (bool?)data.Values[nameof(EnableSymbols)];
                Counter = (double)data.Values[nameof(Counter)];
                
                isRevealChecked = !(bool)data.Values[nameof(RevealChecked)];
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

                isRevealChecked = false;
            }

            RevealChecked(RevealButton, null);

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
