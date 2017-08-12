using System;
using System.Numerics;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Email;
using Windows.ApplicationModel.Resources;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace LessPass
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        #region Acrylic logic
        private SpriteVisual acrylicSprite;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (acrylicSprite != null)
                acrylicSprite.Size = e.NewSize.ToVector2();
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

            CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;

            coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            coreTitleBar.ExtendViewIntoTitleBar = true;
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Thickness padding = InputPanel.Padding;
            padding.Top = sender.Height;
            padding.Bottom = sender.Height;

            InputPanel.Padding = padding;
        }
        #endregion

        #region Dependency properties
        public string Website
        {
            get => (string)GetValue(WebsiteProperty);
            set => SetValue(WebsiteProperty, value);
        }

        public static readonly DependencyProperty WebsiteProperty =
            DependencyProperty.Register("Website", typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

        public string Username
        {
            get => (string)GetValue(UsernameProperty);
            set => SetValue(UsernameProperty, value);
        }

        public static readonly DependencyProperty UsernameProperty =
            DependencyProperty.Register("Username", typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

        public string MasterPassword
        {
            get => (string)GetValue(MasterPasswordProperty);
            set => SetValue(MasterPasswordProperty, value);
        }

        public static readonly DependencyProperty MasterPasswordProperty =
            DependencyProperty.Register("MasterPassword", typeof(string), typeof(MainPage), new PropertyMetadata(string.Empty));

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

        public bool IsValid => Website.Length != 0 && Username.Length != 0 && MasterPassword.Length != 0;

        public MainPage()
        {
            InitializeComponent();

            ElementSoundPlayer.State = ElementSoundPlayerState.On;
        }

        #region Event handlers
        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!IsValid)
                return;

            GeneratedPassword = GetGeneratedPassword();
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsValid)
                return;

            GeneratedPassword = GetGeneratedPassword();
        }

        private string GetGeneratedPassword()
        {
            string salt = string.Concat(Website, Username, Counter.ToString("X"));

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
                return GeneratedPassword;
            }

            return Generator.Generate(MasterPassword, salt, charSets,
                digest: (Generator.Algorithms)AlgorithmCombo.SelectedIndex,
                length: GeneratedPasswordLength,
                iterations: (uint)Iterations);
        }

        private static void OnPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            (sender as MainPage)?.OnPasswordChanged(sender, null);
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
    }
}
