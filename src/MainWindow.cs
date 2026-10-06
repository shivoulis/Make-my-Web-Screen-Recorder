// Main window: pick what to record, audio and options, start recording, and browse recent recordings.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MakeMyWebRecorder {

class MainWindow : Window {
    const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Margin='14' Background='{StaticResource Bg}' CornerRadius='16' BorderBrush='{StaticResource Line}' BorderThickness='1'>
  <Border.Effect><DropShadowEffect BlurRadius='26' ShadowDepth='4' Opacity='0.45'/></Border.Effect>
  <Grid>
    <StackPanel>
      <Grid x:Name='TitleBar' Height='54' Background='Transparent'>
        <StackPanel Orientation='Horizontal' Margin='20,0,0,0' VerticalAlignment='Center' IsHitTestVisible='False'>
          <Grid Width='20' Height='20'>
            <Ellipse Stroke='{StaticResource Text}' StrokeThickness='2'/>
            <Ellipse Width='9' Height='9' Fill='{StaticResource Accent}'/>
          </Grid>
          <TextBlock Text='Make my Web' FontWeight='SemiBold' FontSize='14' Margin='10,0,0,0' VerticalAlignment='Center'/>
          <TextBlock Text=' Screen Recorder' FontSize='14' Foreground='{StaticResource Muted}' VerticalAlignment='Center'/>
        </StackPanel>
        <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,0,10,0'>
          <Button x:Name='BtnSettings' Style='{StaticResource IconButton}' Content='&#xE713;' ToolTip='Settings' AutomationProperties.Name='Settings'/>
          <Button x:Name='BtnMin' Style='{StaticResource IconButton}' Content='&#xE921;' FontSize='10' ToolTip='Minimize' AutomationProperties.Name='Minimize'/>
          <Button x:Name='BtnClose' Style='{StaticResource IconButton}' Content='&#xE8BB;' FontSize='10' ToolTip='Close' AutomationProperties.Name='Close'/>
        </StackPanel>
      </Grid>

      <Border x:Name='UpdateBanner' Visibility='Collapsed' Margin='20,0,20,16' Background='#1B2335' BorderBrush='#2D3B5C' BorderThickness='1' CornerRadius='11' Padding='14,10'>
        <StackPanel>
          <Grid>
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width='Auto'/>
              <ColumnDefinition/>
              <ColumnDefinition Width='Auto'/>
            </Grid.ColumnDefinitions>
            <TextBlock Style='{StaticResource Icon}' Text='&#xE896;' Foreground='#7EA6FF' Margin='0,0,12,0'/>
            <StackPanel Grid.Column='1' VerticalAlignment='Center'>
              <TextBlock x:Name='UpdateTitle' FontWeight='SemiBold' FontSize='13'/>
              <TextBlock x:Name='UpdateSub' FontSize='11.5' Foreground='{StaticResource Muted}'/>
            </StackPanel>
            <StackPanel Grid.Column='2' Orientation='Horizontal' VerticalAlignment='Center'>
              <Button x:Name='BtnWhatsNew' Style='{StaticResource LinkButton}' Content='What&apos;s new' Margin='0,0,4,0'/>
              <Button x:Name='BtnUpdate' Style='{StaticResource PrimaryButton}' Height='32' Width='84' FontSize='13' Content='Update' AutomationProperties.Name='Update now'/>
            </StackPanel>
          </Grid>
          <ProgressBar x:Name='UpdateBar' Margin='0,10,0,0' Maximum='100' Visibility='Collapsed'/>
        </StackPanel>
      </Border>

      <StackPanel Margin='20,0,20,20'>
        <TextBlock Text='WHAT TO RECORD' Style='{StaticResource Caption}'/>
        <Border Background='{StaticResource Card}' CornerRadius='12' Padding='4' Margin='0,8,0,0'>
          <UniformGrid Columns='4'>
            <RadioButton x:Name='ModeScreen' GroupName='mode' Style='{StaticResource Segment}' Tag='&#xE7F4;' Content='Screen'/>
            <RadioButton x:Name='ModeWindow' GroupName='mode' Style='{StaticResource Segment}' Tag='&#xE7C4;' Content='Window'/>
            <RadioButton x:Name='ModeArea' GroupName='mode' Style='{StaticResource Segment}' Tag='&#xE7A8;' Content='Area'/>
            <RadioButton x:Name='ModeTab' GroupName='mode' Style='{StaticResource Segment}' Tag='&#xE774;' Content='Browser tab'/>
          </UniformGrid>
        </Border>
        <Grid x:Name='SourceRow' Margin='0,10,0,0'>
          <Grid.ColumnDefinitions>
            <ColumnDefinition/>
            <ColumnDefinition Width='Auto'/>
          </Grid.ColumnDefinitions>
          <ComboBox x:Name='CbSource'>
            <ComboBox.ItemTemplate>
              <DataTemplate>
                <Grid>
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition/>
                    <ColumnDefinition Width='Auto'/>
                  </Grid.ColumnDefinitions>
                  <TextBlock Text='{Binding Label}' TextTrimming='CharacterEllipsis' FontSize='13'/>
                  <TextBlock Grid.Column='1' Text='{Binding Detail}' Foreground='{StaticResource Muted}' FontSize='11.5' Margin='10,0,0,0' VerticalAlignment='Center'/>
                </Grid>
              </DataTemplate>
            </ComboBox.ItemTemplate>
          </ComboBox>
          <Button x:Name='BtnRefresh' Grid.Column='1' Style='{StaticResource IconButton}' Content='&#xE72C;' Width='40' Height='40' Margin='8,0,0,0' ToolTip='Refresh list' AutomationProperties.Name='Refresh list'/>
        </Grid>
        <Border x:Name='AreaRow' Background='{StaticResource Card2}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='10'
                Height='40' Margin='0,10,0,0' Visibility='Collapsed'>
          <Grid>
            <StackPanel Orientation='Horizontal' VerticalAlignment='Center' Margin='13,0,0,0'>
              <TextBlock Style='{StaticResource Icon}' Text='&#xE7A8;' FontSize='14' Foreground='{StaticResource Muted}' Margin='0,0,10,0'/>
              <TextBlock x:Name='AreaText' FontSize='13' VerticalAlignment='Center'/>
            </StackPanel>
            <Button x:Name='BtnPickArea' Style='{StaticResource LinkButton}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,6,0'
                    Content='Select area' AutomationProperties.Name='Select area'/>
          </Grid>
        </Border>
        <TextBlock x:Name='SourceHint' Style='{StaticResource Hint}' Margin='2,8,0,0'/>

        <TextBlock Text='CAMERA, AUDIO &amp; SUBTITLES' Style='{StaticResource Caption}' Margin='0,22,0,0'/>
        <Border Background='{StaticResource Card}' CornerRadius='12' Padding='16,4,16,6' Margin='0,8,0,0'>
          <StackPanel>
            <CheckBox x:Name='ChkCam' AutomationProperties.Name='Camera' Style='{StaticResource Switch}'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Style='{StaticResource Icon}' Text='&#xE714;' Margin='0,0,12,0'/>
                <StackPanel>
                  <TextBlock Text='Camera'/>
                  <TextBlock Text='Round video bubble in the corner, drag to move' FontSize='11.5' Foreground='{StaticResource Muted}'/>
                </StackPanel>
              </StackPanel>
            </CheckBox>
            <Grid x:Name='CamRow' Margin='0,2,0,10' Visibility='Collapsed'>
              <Grid.ColumnDefinitions>
                <ColumnDefinition/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <ComboBox x:Name='CbCam' Height='36'>
                <ComboBox.ItemTemplate>
                  <DataTemplate><TextBlock Text='{Binding}' TextTrimming='CharacterEllipsis' FontSize='12.5'/></DataTemplate>
                </ComboBox.ItemTemplate>
              </ComboBox>
              <Button x:Name='BtnEffects' Grid.Column='1' Style='{StaticResource LinkButton}' VerticalAlignment='Center' Margin='6,0,-8,0'
                      AutomationProperties.Name='Camera effects' ToolTip='Background blur, scenes and filters'>
                <StackPanel Orientation='Horizontal'>
                  <TextBlock Text='&#xE790;' FontFamily='{StaticResource Icons}' FontSize='13' VerticalAlignment='Center' Margin='0,0,6,0'/>
                  <TextBlock x:Name='EffectsText' Text='Effects' VerticalAlignment='Center'/>
                </StackPanel>
              </Button>
            </Grid>
            <Border Height='1' Background='{StaticResource Line}' Margin='-16,0,-16,4'/>
            <CheckBox x:Name='ChkMic' AutomationProperties.Name='Microphone' Style='{StaticResource Switch}'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Style='{StaticResource Icon}' Text='&#xE720;' Margin='0,0,12,0'/>
                <TextBlock Text='Microphone' VerticalAlignment='Center'/>
              </StackPanel>
            </CheckBox>
            <ComboBox x:Name='CbMic' Height='36' Margin='0,2,0,10'>
              <ComboBox.ItemTemplate>
                <DataTemplate><TextBlock Text='{Binding}' TextTrimming='CharacterEllipsis' FontSize='12.5'/></DataTemplate>
              </ComboBox.ItemTemplate>
            </ComboBox>
            <Border Height='1' Background='{StaticResource Line}' Margin='-16,0'/>
            <CheckBox x:Name='ChkSys' AutomationProperties.Name='System audio' Style='{StaticResource Switch}' Margin='0,6,0,0'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Style='{StaticResource Icon}' Text='&#xE767;' Margin='0,0,12,0'/>
                <StackPanel>
                  <TextBlock Text='System audio'/>
                  <TextBlock Text='Sound from apps, videos and calls' FontSize='11.5' Foreground='{StaticResource Muted}'/>
                </StackPanel>
              </StackPanel>
            </CheckBox>
            <Border Height='1' Background='{StaticResource Line}' Margin='-16,6,-16,4'/>
            <CheckBox x:Name='ChkSubs' AutomationProperties.Name='Subtitles' Style='{StaticResource Switch}'>
              <StackPanel Orientation='Horizontal'>
                <TextBlock Style='{StaticResource Icon}' Text='&#xE7F0;' Margin='0,0,12,0'/>
                <StackPanel>
                  <TextBlock Text='Subtitles'/>
                  <TextBlock Text='Transcribed automatically, any language' FontSize='11.5' Foreground='{StaticResource Muted}'/>
                </StackPanel>
              </StackPanel>
            </CheckBox>
            <StackPanel x:Name='SubOptions' Margin='30,0,0,8' Visibility='Collapsed'>
              <Border HorizontalAlignment='Left' Background='{StaticResource Card2}' CornerRadius='9' Padding='3'>
                <StackPanel Orientation='Horizontal'>
                  <RadioButton x:Name='SubTrack' GroupName='substyle' Style='{StaticResource Pill}' Content='Subtitle track + .srt' ToolTip='Viewers can switch the subtitles on or off'/>
                  <RadioButton x:Name='SubBurn' GroupName='substyle' Style='{StaticResource Pill}' Content='Burned into video' ToolTip='Always visible, e.g. for social media'/>
                </StackPanel>
              </Border>
              <Grid Margin='0,8,0,0'>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width='Auto'/>
                  <ColumnDefinition/>
                </Grid.ColumnDefinitions>
                <TextBlock Text='Spoken language' FontSize='12.5' Foreground='{StaticResource Muted}' VerticalAlignment='Center' Margin='2,0,12,0'/>
                <ComboBox x:Name='CbLang' Grid.Column='1' Height='32' AutomationProperties.Name='Spoken language'
                          ToolTip='The main language you speak. Words in other languages (e.g. English terms) are still written.'/>
              </Grid>
            </StackPanel>
          </StackPanel>
        </Border>

        <Button x:Name='BtnRecord' Style='{StaticResource PrimaryButton}' Margin='0,20,0,0' AutomationProperties.Name='Start recording'>
          <StackPanel Orientation='Horizontal'>
            <Ellipse Width='12' Height='12' Fill='White' Margin='0,0,10,0' VerticalAlignment='Center'/>
            <TextBlock x:Name='RecordText' Text='Start recording' VerticalAlignment='Center'/>
          </StackPanel>
        </Button>

        <Grid Margin='0,24,0,0'>
          <TextBlock Text='RECORDINGS' Style='{StaticResource Caption}' VerticalAlignment='Center'/>
          <StackPanel Orientation='Horizontal' HorizontalAlignment='Right' Margin='0,-6,-8,-6'>
            <Button x:Name='BtnFolder' Style='{StaticResource LinkButton}' Content='Change folder'/>
            <Button x:Name='BtnOpenFolder' Style='{StaticResource LinkButton}' Content='Open folder'/>
          </StackPanel>
        </Grid>
        <TextBlock x:Name='FolderPath' Style='{StaticResource Hint}' TextWrapping='NoWrap' TextTrimming='CharacterEllipsis' FontSize='11.5' Margin='2,2,0,0'/>
        <Border x:Name='BusyCard' Background='{StaticResource Card}' CornerRadius='11' Padding='14,12' Margin='0,10,0,0' Visibility='Collapsed'>
          <StackPanel>
            <Grid>
              <TextBlock x:Name='BusyText' Text='Compressing recording' FontSize='13'/>
              <TextBlock x:Name='BusyPct' HorizontalAlignment='Right' Foreground='{StaticResource Muted}' FontSize='12'/>
            </Grid>
            <ProgressBar x:Name='BusyBar' Margin='0,9,0,0' Maximum='100'/>
          </StackPanel>
        </Border>
        <StackPanel x:Name='RecentList' Margin='0,10,0,0'/>
        <TextBlock x:Name='EmptyText' Style='{StaticResource Hint}' Text='Your recordings will appear here.' Margin='2,4,0,0'/>
      </StackPanel>
    </StackPanel>

    <Popup x:Name='SettingsPopup' PlacementTarget='{Binding ElementName=BtnSettings}' Placement='Bottom' HorizontalOffset='-262'
           StaysOpen='False' AllowsTransparency='True' PopupAnimation='Fade'>
      <Border Background='#1F232B' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='12' Padding='16,8' Width='310' Margin='0,6,10,10'>
        <Border.Effect><DropShadowEffect BlurRadius='18' ShadowDepth='3' Opacity='0.4'/></Border.Effect>
        <StackPanel TextElement.Foreground='{StaticResource Text}'>
          <Grid Height='44'>
            <TextBlock Text='Frame rate' VerticalAlignment='Center' FontSize='13.5'/>
            <Border HorizontalAlignment='Right' VerticalAlignment='Center' Background='{StaticResource Card}' CornerRadius='9' Padding='3'>
              <StackPanel Orientation='Horizontal'>
                <RadioButton x:Name='Fps30' GroupName='fps' Style='{StaticResource Pill}' Content='30 fps'/>
                <RadioButton x:Name='Fps60' GroupName='fps' Style='{StaticResource Pill}' Content='60 fps'/>
              </StackPanel>
            </Border>
          </Grid>
          <Grid Height='44'>
            <TextBlock Text='Camera size' VerticalAlignment='Center' FontSize='13.5'/>
            <Border HorizontalAlignment='Right' VerticalAlignment='Center' Background='{StaticResource Card}' CornerRadius='9' Padding='3'>
              <StackPanel Orientation='Horizontal'>
                <RadioButton x:Name='CamS' GroupName='camsize' Style='{StaticResource Pill}' Content='S'/>
                <RadioButton x:Name='CamM' GroupName='camsize' Style='{StaticResource Pill}' Content='M'/>
                <RadioButton x:Name='CamL' GroupName='camsize' Style='{StaticResource Pill}' Content='L'/>
              </StackPanel>
            </Border>
          </Grid>
          <Grid Height='44'>
            <StackPanel VerticalAlignment='Center'>
              <TextBlock Text='Subtitle accuracy' FontSize='13.5'/>
              <TextBlock x:Name='SubModelHint' FontSize='11' Foreground='{StaticResource Muted}'/>
            </StackPanel>
            <Border HorizontalAlignment='Right' VerticalAlignment='Center' Background='{StaticResource Card}' CornerRadius='9' Padding='3'>
              <StackPanel Orientation='Horizontal'>
                <RadioButton x:Name='SubFast' GroupName='submodel' Style='{StaticResource Pill}' Content='Fast'/>
                <RadioButton x:Name='SubAccurate' GroupName='submodel' Style='{StaticResource Pill}' Content='Accurate'/>
              </StackPanel>
            </Border>
          </Grid>
          <CheckBox x:Name='ChkCursor' Style='{StaticResource Switch}' Content='Show mouse cursor'/>
          <CheckBox x:Name='ChkClicks' Style='{StaticResource Switch}' Content='Highlight mouse clicks'/>
          <CheckBox x:Name='ChkCountdown' Style='{StaticResource Switch}' Content='3-second countdown'/>
          <CheckBox x:Name='ChkUpdates' Style='{StaticResource Switch}' Content='Check for updates automatically'/>
          <CheckBox x:Name='ChkKeep' AutomationProperties.Name='Keep uncompressed original' Style='{StaticResource Switch}'>
            <StackPanel Margin='0,4'>
              <TextBlock Text='Keep uncompressed original'/>
              <TextBlock Text='Truly lossless, but very large files' FontSize='11.5' Foreground='{StaticResource Muted}'/>
            </StackPanel>
          </CheckBox>
          <Border Height='1' Background='{StaticResource Line}' Margin='-16,6,-16,6'/>
          <Grid Height='36'>
            <TextBlock x:Name='VersionText' VerticalAlignment='Center' FontSize='12' Foreground='{StaticResource Muted}'/>
            <Button x:Name='BtnCheckUpdates' Style='{StaticResource LinkButton}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,-8,0' Content='Check for updates'/>
          </Grid>
        </StackPanel>
      </Border>
    </Popup>
  </Grid>
</Border>";

    readonly FrameworkElement root;
    readonly Settings settings = Settings.Load();
    readonly double scale;
    readonly DispatcherTimer timer = new DispatcherTimer();
    readonly System.Windows.Forms.NotifyIcon tray = new System.Windows.Forms.NotifyIcon();

    List<SrcItem> outputs = new List<SrcItem>();
    int refreshVersion;
    bool starting;
    Session session;
    ControlBar bar;
    RegionFrame frame;
    Job job;
    UpdateInfo update;
    CameraBubble camera;
    readonly CameraEffects camFx = new CameraEffects();
    bool downloadingModel;
    bool updating;

    T F<T>(string name) { return (T)root.FindName(name); }

    public MainWindow() {
        using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96.0;
        Overlay.Scale = scale;

        Title = Program.AppName;
        Width = 468;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = (FontFamily)FindResource("UiFont");
        Foreground = (Brush)FindResource("Text");
        UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        System.Drawing.Icon appIcon = null;
        try {
            appIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            Icon = Imaging.CreateBitmapSourceFromHIcon(appIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        } catch { }
        tray.Icon = appIcon ?? System.Drawing.SystemIcons.Application;
        tray.Text = "Recording - click to stop";
        tray.MouseClick += delegate { StopRecording(); };

        root = (FrameworkElement)XamlReader.Parse(Xaml);
        Content = root;

        // Title bar
        F<Grid>("TitleBar").MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        F<Button>("BtnMin").Click += delegate { WindowState = WindowState.Minimized; };
        F<Button>("BtnClose").Click += delegate { Close(); };
        F<Button>("BtnSettings").Click += delegate { var p = F<Popup>("SettingsPopup"); p.IsOpen = !p.IsOpen; };

        // Source
        F<RadioButton>(settings.Mode == "window" ? "ModeWindow" : settings.Mode == "tab" ? "ModeTab" : settings.Mode == "area" ? "ModeArea" : "ModeScreen").IsChecked = true;
        foreach (var n in new[] { "ModeScreen", "ModeWindow", "ModeArea", "ModeTab" })
            F<RadioButton>(n).Checked += delegate { settings.Mode = Mode(); RefreshSources(); };
        F<Button>("BtnRefresh").Click += delegate { RefreshSources(); };
        F<Button>("BtnPickArea").Click += async delegate { await PickArea(); };
        F<ComboBox>("CbSource").DropDownOpened += delegate { if (Mode() == "window") RefreshSources(); };

        // Audio
        var cbMic = F<ComboBox>("CbMic");
        cbMic.ItemsSource = new[] { "Looking for microphones..." };
        cbMic.SelectedIndex = 0;
        cbMic.IsEnabled = false;
        F<CheckBox>("ChkMic").IsChecked = settings.MicOn;
        F<CheckBox>("ChkMic").Checked += delegate { UpdateUi(); };
        F<CheckBox>("ChkMic").Unchecked += delegate { UpdateUi(); };
        F<CheckBox>("ChkSys").IsChecked = settings.SystemAudio;
        F<CheckBox>("ChkCam").IsChecked = settings.Camera;
        camFx.Background = settings.CamBackground;
        camFx.Scene = settings.CamScene;
        camFx.CustomImage = settings.CamImage;
        camFx.Filter = settings.CamFilter;
        F<Button>("BtnEffects").Click += delegate {
            var dev = F<ComboBox>("CbCam").SelectedItem as string;
            if (dev == null || session != null) return;
            new EffectsWindow(this, dev, camFx, scale).ShowDialog();
            SaveSettings();
            UpdateUi();
        };
        F<CheckBox>("ChkCam").Checked += delegate { UpdateUi(); };
        F<CheckBox>("ChkCam").Unchecked += delegate { UpdateUi(); };
        F<CheckBox>("ChkSubs").IsChecked = settings.Subtitles;
        F<CheckBox>("ChkSubs").Checked += async delegate {
            UpdateUi();
            if (!Subtitles.HasModel(SubModel()) && !await EnsureModel(SubModel())) F<CheckBox>("ChkSubs").IsChecked = false;
        };
        F<CheckBox>("ChkSubs").Unchecked += delegate { UpdateUi(); };
        F<RadioButton>(settings.SubStyle == Subtitles.Burn ? "SubBurn" : "SubTrack").IsChecked = true;
        var langs = Subtitles.Languages.Select(l => new LangChoice { Code = l[0], Name = l[1] }).ToList();
        F<ComboBox>("CbLang").ItemsSource = langs;
        F<ComboBox>("CbLang").SelectedItem = langs.FirstOrDefault(l => l.Code == settings.SubLang) ?? langs[0];
        F<RadioButton>(settings.SubModel == Subtitles.Fast ? "SubFast" : "SubAccurate").IsChecked = true;
        F<RadioButton>(settings.CameraSize == "S" ? "CamS" : settings.CameraSize == "L" ? "CamL" : "CamM").IsChecked = true;
        foreach (var n in new[] { "SubFast", "SubAccurate" }) F<RadioButton>(n).Checked += delegate { UpdateUi(); };
        LoadDevices();

        // Options
        F<RadioButton>(settings.Fps == "60" ? "Fps60" : "Fps30").IsChecked = true;
        F<CheckBox>("ChkCursor").IsChecked = settings.Cursor;
        F<CheckBox>("ChkClicks").IsChecked = settings.Clicks;
        F<CheckBox>("ChkCountdown").IsChecked = settings.Countdown;
        F<CheckBox>("ChkKeep").IsChecked = settings.KeepLossless;
        F<CheckBox>("ChkUpdates").IsChecked = settings.AutoUpdate;
        F<TextBlock>("VersionText").Text = "Version " + Updater.Pretty(Updater.Current);
        F<Button>("BtnCheckUpdates").Click += delegate { F<Popup>("SettingsPopup").IsOpen = false; CheckForUpdates(false); };
        F<Button>("BtnUpdate").Click += delegate { InstallUpdate(); };
        F<Button>("BtnWhatsNew").Click += delegate { if (update != null) try { Process.Start(update.PageUrl); } catch { } };

        // Actions
        F<Button>("BtnRecord").Click += delegate { StartRecording(); };
        F<Button>("BtnFolder").Click += delegate { ChangeFolder(); };
        F<Button>("BtnOpenFolder").Click += delegate {
            try { Directory.CreateDirectory(settings.Folder); Process.Start("explorer.exe", "\"" + settings.Folder + "\""); } catch { }
        };

        Activated += delegate { if (session == null) RefreshRecents(); };
        Closing += OnClosing;
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += delegate { Tick(); };
        timer.Start();

        RefreshSources();
        RefreshRecents();
        Task.Run(() => Updater.CleanUp());
        ContentRendered += delegate {
            if (!settings.UpdateAsked) {
                // Ask once before contacting the internet (see CODE_SIGNING.md, privacy policy).
                var r = MessageBox.Show(this, "Check for updates automatically when the app starts?\n\nThe app will ask GitHub whether a newer version is available. No personal data or recordings are sent. You can change this later in Settings.",
                    Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                settings.AutoUpdate = r == MessageBoxResult.Yes;
                settings.UpdateAsked = true;
                F<CheckBox>("ChkUpdates").IsChecked = settings.AutoUpdate;
                settings.Save();
            }
            if (settings.AutoUpdate) CheckForUpdates(true);
        };
    }

    // ---- helpers ---------------------------------------------------------------------------

    string Mode() {
        if (F<RadioButton>("ModeWindow").IsChecked == true) return "window";
        if (F<RadioButton>("ModeTab").IsChecked == true) return "tab";
        if (F<RadioButton>("ModeArea").IsChecked == true) return "area";
        return "screen";
    }

    bool On(string name) { return F<CheckBox>(name).IsChecked == true; }

    string SubModel() { return F<RadioButton>("SubFast").IsChecked == true ? Subtitles.Fast : Subtitles.Accurate; }
    string SubStyle() { return F<RadioButton>("SubBurn").IsChecked == true ? Subtitles.Burn : Subtitles.Track; }
    string SubLang() { var l = F<ComboBox>("CbLang").SelectedItem as LangChoice; return l == null ? "en" : l.Code; }

    class LangChoice {
        public string Code, Name;
        public override string ToString() { return Name; }
    }

    void Error(string msg) {
        if (IsVisible) MessageBox.Show(this, msg, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        else MessageBox.Show(msg, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    void SaveSettings() {
        settings.Mode = Mode();
        settings.MicOn = On("ChkMic");
        var mic = F<ComboBox>("CbMic");
        if (mic.IsEnabled && mic.SelectedItem != null) settings.Mic = (string)mic.SelectedItem;
        settings.SystemAudio = On("ChkSys");
        settings.Fps = F<RadioButton>("Fps60").IsChecked == true ? "60" : "30";
        settings.Cursor = On("ChkCursor");
        settings.Clicks = On("ChkClicks");
        settings.Countdown = On("ChkCountdown");
        settings.KeepLossless = On("ChkKeep");
        settings.AutoUpdate = On("ChkUpdates");
        settings.Camera = On("ChkCam");
        var cam = F<ComboBox>("CbCam");
        if (cam.Tag as string == "ready" && cam.SelectedItem != null) settings.CameraDevice = (string)cam.SelectedItem;
        settings.CameraSize = F<RadioButton>("CamS").IsChecked == true ? "S" : F<RadioButton>("CamL").IsChecked == true ? "L" : "M";
        settings.Subtitles = On("ChkSubs");
        settings.CamBackground = camFx.Background;
        settings.CamScene = camFx.Scene;
        settings.CamImage = camFx.CustomImage;
        settings.CamFilter = camFx.Filter;
        settings.SubStyle = SubStyle();
        settings.SubModel = SubModel();
        settings.SubLang = SubLang();
        settings.Save();
    }

    void UpdateUi() {
        var src = F<ComboBox>("CbSource");
        bool busy = job != null;
        var btn = F<Button>("BtnRecord");
        btn.IsEnabled = !starting && session == null && !busy && (Mode() == "area" || src.SelectedItem is SrcItem);
        F<TextBlock>("RecordText").Text = busy ? "Please wait - " + job.Verb.ToLowerInvariant() + "..." : "Start recording";
        var cbMic = F<ComboBox>("CbMic");
        bool haveMics = cbMic.Tag as string == "ready";
        cbMic.IsEnabled = haveMics && On("ChkMic");
        F<CheckBox>("ChkMic").IsEnabled = haveMics;
        F<TextBlock>("FolderPath").Text = settings.Folder;
        var cbCam = F<ComboBox>("CbCam");
        bool haveCams = cbCam.Tag as string == "ready";
        F<CheckBox>("ChkCam").IsEnabled = haveCams;
        F<Grid>("CamRow").Visibility = haveCams && On("ChkCam") ? Visibility.Visible : Visibility.Collapsed;
        F<TextBlock>("EffectsText").Text = camFx.Background == "none" && camFx.Filter == "none" ? "Effects"
            : camFx.Background == "blur" ? "Blur" : camFx.Background == "scene" ? SceneImages.Title(camFx.Scene) : "Filter";
        F<StackPanel>("SubOptions").Visibility = On("ChkSubs") ? Visibility.Visible : Visibility.Collapsed;
        F<TextBlock>("SubModelHint").Text = SubModel() == Subtitles.Fast ? "Quicker, less accurate" : "Best for Greek, slower";
        if (downloadingModel) btn.IsEnabled = false;
    }

    async void LoadDevices() {
        List<string> mics = new List<string>(), cams = new List<string>();
        try { await Task.Run(() => FF.Devices(out mics, out cams)); } catch { }
        var cc = F<ComboBox>("CbCam");
        if (cams.Count == 0) {
            cc.ItemsSource = new[] { "No camera found" };
            cc.SelectedIndex = 0;
            F<CheckBox>("ChkCam").IsChecked = false;
        } else {
            cc.ItemsSource = cams;
            cc.SelectedItem = cams.Contains(settings.CameraDevice) ? settings.CameraDevice : cams[0];
            cc.Tag = "ready";
        }
        var cb = F<ComboBox>("CbMic");
        if (mics.Count == 0) {
            cb.ItemsSource = new[] { "No microphone found" };
            cb.SelectedIndex = 0;
            F<CheckBox>("ChkMic").IsChecked = false;
        } else {
            cb.ItemsSource = mics;
            cb.SelectedItem = mics.Contains(settings.Mic) ? settings.Mic : mics[0];
            cb.Tag = "ready";
        }
        UpdateUi();
    }

    async void RefreshSources() {
        int version = ++refreshVersion;
        string mode = Mode();
        var cb = F<ComboBox>("CbSource");
        var hint = F<TextBlock>("SourceHint");
        string prev = cb.SelectedItem is SrcItem && ((SrcItem)cb.SelectedItem).Kind == (mode == "screen" ? "screen" : mode)
            ? ((SrcItem)cb.SelectedItem).Label : null;

        outputs = Native.Outputs();
        bool area = mode == "area";
        F<Grid>("SourceRow").Visibility = area ? Visibility.Collapsed : Visibility.Visible;
        F<Border>("AreaRow").Visibility = area ? Visibility.Visible : Visibility.Collapsed;
        List<SrcItem> items;
        if (area) {
            items = new List<SrcItem>();
            var a = SavedArea();
            F<TextBlock>("AreaText").Text = a == null ? "No area selected yet" : a.W + " × " + a.H + " area";
            F<Button>("BtnPickArea").Content = a == null ? "Select area" : "Change";
            hint.Text = a == null ? "Drag across the screen to choose the part to record."
                                  : "Records this part of the screen. Press Start to record it, or Change to pick another.";
            if (outputs.Count == 0) hint.Text = "Recording part of the screen needs Windows 10 or newer with a working graphics driver.";
            cb.ItemsSource = items;
            UpdateUi();
            return;
        } else if (mode == "tab") {
            hint.Text = "Looking for browser tabs...";
            cb.IsEnabled = false;
            items = await Task.Run(() => BrowserTabs.List());
            if (version != refreshVersion) return;
            cb.IsEnabled = true;
            hint.Text = items.Count == 0
                ? "No Chrome, Edge or Brave windows are open. Open one, then press refresh."
                : "Switches to the tab and records only the web page, without the browser's toolbars.";
        } else if (mode == "window") {
            items = Native.Windows();
            hint.Text = items.Count == 0 ? "No windows found." : "Records the window's area. Keep it in place while recording.";
        } else {
            items = new List<SrcItem>();
            if (outputs.Count == 0) {
                var d = new SrcItem { Kind = "desktop", Label = "Entire screen" };
                items.Add(d);
            }
            int n = 1;
            foreach (var o in outputs) {
                o.Label = outputs.Count == 1 ? "Entire screen" : "Screen " + n;
                o.Detail = o.W + " x " + o.H + (o.X == 0 && o.Y == 0 && outputs.Count > 1 ? " - main" : "");
                items.Add(o);
                n++;
            }
            hint.Text = "Records everything on " + (outputs.Count > 1 ? "the chosen screen." : "your screen.");
        }
        if (mode != "screen" && outputs.Count == 0) {
            items.Clear();
            hint.Text = "Recording a single window or tab needs Windows 10 or newer with a working graphics driver.";
        }
        cb.ItemsSource = items;
        var match = items.FirstOrDefault(i => i.Label == prev);
        cb.SelectedItem = match ?? items.FirstOrDefault();
        UpdateUi();
    }

    // ---- updates ------------------------------------------------------------------------------

    async void CheckForUpdates(bool quiet) {
        try {
            update = await Task.Run(() => Updater.Check());
        } catch (Exception e) {
            if (!quiet) Error("Couldn't check for updates:\n" + e.Message);
            return;
        }
        if (update == null) {
            F<Border>("UpdateBanner").Visibility = Visibility.Collapsed;
            if (!quiet) MessageBox.Show(this, "You're up to date (version " + Updater.Pretty(Updater.Current) + ").", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        F<TextBlock>("UpdateTitle").Text = "Version " + Updater.Pretty(update.Version) + " is available";
        F<TextBlock>("UpdateSub").Text = "You have " + Updater.Pretty(Updater.Current) + ". The update takes a few seconds.";
        F<Border>("UpdateBanner").Visibility = Visibility.Visible;
    }

    async void InstallUpdate() {
        if (update == null || updating) return;
        if (session != null || starting || job != null) {
            Error("Please wait until the current recording" + (job != null ? " has finished " + job.Verb.ToLowerInvariant() : " is finished") + ", then update.");
            return;
        }
        updating = true;
        var btn = F<Button>("BtnUpdate");
        var bar = F<ProgressBar>("UpdateBar");
        btn.IsEnabled = false;
        bar.Visibility = Visibility.Visible;
        F<TextBlock>("UpdateSub").Text = "Downloading...";
        try {
            string installer = await Updater.Download(update, p => Dispatcher.BeginInvoke(new Action(() => bar.Value = p)));
            F<TextBlock>("UpdateSub").Text = "Installing... the app will restart.";
            SaveSettings();
            Updater.Install(installer);
            await Task.Delay(500);
            Application.Current.Shutdown();
        } catch (Exception e) {
            updating = false;
            btn.IsEnabled = true;
            bar.Visibility = Visibility.Collapsed;
            F<TextBlock>("UpdateSub").Text = "You have " + Updater.Pretty(Updater.Current) + ".";
            Error("The update couldn't be installed:\n" + e.Message);
        }
    }

    SrcItem SavedArea() {
        var p = (settings.Area ?? "").Split(',');
        int x, y, w, h;
        if (p.Length != 4 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w) || !int.TryParse(p[3], out h)) return null;
        if (w < 16 || h < 16) return null;
        return new SrcItem { Kind = "area", X = x, Y = y, W = w, H = h, Label = w + " x " + h + " area" };
    }

    // Lets the user drag out an area on screen. Returns true if one was chosen.
    async Task<bool> PickArea() {
        if (outputs.Count == 0) outputs = Native.Outputs();
        Hide();
        await Task.Delay(250);     // let this window disappear before the screen is captured
        bool ok = false;
        using (var picker = new AreaPicker(outputs, scale)) {
            if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                var r = picker.Selected;
                settings.Area = r.X + "," + r.Y + "," + r.Width + "," + r.Height;
                settings.Save();
                ok = true;
            }
        }
        Show();
        Activate();
        RefreshSources();
        return ok;
    }

    // ---- recording -------------------------------------------------------------------------

    // Works out which monitor to capture and the area on it. Returns an error message or null.
    string Resolve(SrcItem src, out SrcItem monitor, out int[] region, out int[] abs) {
        monitor = null; region = null; abs = null;
        if (src.Kind == "desktop") {
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            monitor = new SrcItem { Kind = "desktop", X = vs.X, Y = vs.Y, W = vs.Width, H = vs.Height };
            abs = new[] { vs.X, vs.Y, vs.Width, vs.Height };
            return null;
        }
        if (src.Kind == "screen") {
            monitor = src;
            abs = new[] { src.X, src.Y, src.W, src.H };
            return null;
        }
        bool tab = src.Kind == "tab";
        if (src.Kind == "area") {
            int[] ab = { src.X, src.Y, src.W, src.H };
            foreach (var o in outputs)
                if (src.X >= o.X && src.X < o.X + o.W && src.Y >= o.Y && src.Y < o.Y + o.H) { monitor = o; break; }
            if (monitor == null) return "The selected area is no longer on screen. Please select it again.";
            return ClipToMonitor(ab, monitor, out region, out abs);
        }
        if (!Native.IsWindow(src.Handle)) return tab ? "That browser window has closed." : "That window has closed. Refresh the list and pick it again.";
        int[] b = tab ? BrowserTabs.PageRect(src.Handle) : Native.Bounds(src.Handle);
        if (b == null) return "Could not find that " + (tab ? "tab" : "window") + " on screen.";
        int cx = b[0] + b[2] / 2, cy = b[1] + b[3] / 2;
        foreach (var o in outputs)
            if (cx >= o.X && cx < o.X + o.W && cy >= o.Y && cy < o.Y + o.H) { monitor = o; break; }
        if (monitor == null) return "That " + (tab ? "tab" : "window") + " is not visible on screen, so it can't be recorded.";
        string err = ClipToMonitor(b, monitor, out region, out abs);
        return err == null ? null : "That " + (tab ? "tab" : "window") + " is too small or off screen.";
    }

    // Clips rectangle b to the monitor; region is relative to the monitor, abs is in screen coordinates.
    static string ClipToMonitor(int[] b, SrcItem monitor, out int[] region, out int[] abs) {
        region = null; abs = null;
        int x1 = Math.Max(b[0], monitor.X), y1 = Math.Max(b[1], monitor.Y);
        int x2 = Math.Min(b[0] + b[2], monitor.X + monitor.W), y2 = Math.Min(b[1] + b[3], monitor.Y + monitor.H);
        int w = (x2 - x1) - (x2 - x1) % 2, h = (y2 - y1) - (y2 - y1) % 2;
        if (w < 16 || h < 16) return "The area is too small or off screen.";
        region = new[] { x1 - monitor.X, y1 - monitor.Y, w, h };
        abs = new[] { x1, y1, w, h };
        return null;
    }

    async void StartRecording() {
        if (session != null || starting || job != null) return;
        if (Mode() == "area" && SavedArea() == null && !await PickArea()) return;
        var src = Mode() == "area" ? SavedArea() : F<ComboBox>("CbSource").SelectedItem as SrcItem;
        if (src == null) return;
        SaveSettings();
        starting = true;
        UpdateUi();
        try {
            if (src.Kind == "tab" && !BrowserTabs.Activate(src)) {
                Error("That tab is no longer open. Press refresh and pick it again.");
                return;
            }
            if (src.Kind == "window") {
                if (!Native.IsWindow(src.Handle)) { Error("That window has closed. Refresh the list and pick it again."); return; }
                Native.BringToFront(src.Handle);
            }
            Hide();
            await Task.Delay(src.Kind == "tab" ? 700 : 350);

            SrcItem monitor; int[] region, abs;
            string err = Resolve(src, out monitor, out region, out abs);
            if (err == null && On("ChkCam") && F<ComboBox>("CbCam").Tag as string == "ready") {
                // Start the camera early so it is live by the time recording begins.
                if (camFx.NeedsModel && !SegmentationModel.Available) try { await SegmentationModel.Download(); } catch { }
                camera = new CameraBubble((string)F<ComboBox>("CbCam").SelectedItem, settings.CameraSize, scale, camFx);
                camera.ShowInCorner(abs, scale);
            }
            if (err == null && On("ChkCountdown")) {
                await Overlay.Countdown(monitor);
                err = Resolve(src, out monitor, out region, out abs);     // the window may have moved
            }
            if (err == null && camera != null) {
                await Task.WhenAny(camera.Ready, Task.Delay(5000));
                if (camera.Ready.IsCompleted && camera.Ready.Result != null) err = camera.Ready.Result;
            }
            if (err != null) { CloseOverlays(); Show(); Error(err); return; }

            var o = new RecordOptions();
            o.Screen = monitor;
            o.Region = region;
            o.Mic = On("ChkMic") && F<ComboBox>("CbMic").IsEnabled ? (string)F<ComboBox>("CbMic").SelectedItem : null;
            o.SystemAudio = On("ChkSys");
            o.Cursor = On("ChkCursor");
            o.Fps = settings.Fps == "60" ? 60 : 30;
            o.Folder = settings.Folder;
            session = Session.Start(o, out err);
            if (session == null) { CloseOverlays(); Show(); Error(err); return; }

            frame = new RegionFrame(abs, src.Kind == "screen" || src.Kind == "desktop");
            bar = new ControlBar(session.HasMic);
            bar.PauseClicked += TogglePause;
            bar.MicClicked += delegate { if (session != null) { session.SetMicMuted(!session.MicMuted); Tick(); } };
            bar.StopClicked += StopRecording;
            bar.DiscardClicked += DiscardRecording;
            bar.ShowOn(monitor);
            tray.Visible = true;
            if (On("ChkClicks")) ClickEffects.Start(scale);
        } catch (Exception e) {
            CloseOverlays();
            Show();
            Error("Could not start recording:\n" + e.Message);
        } finally {
            starting = false;
            UpdateUi();
        }
    }

    void TogglePause() {
        if (session == null) return;
        if (session.Paused) session.Resume(); else session.Pause();
        if (frame != null) frame.SetPaused(session.Paused);
        Tick();
    }

    void CloseOverlays() {
        ClickEffects.Stop();
        tray.Visible = false;
        if (bar != null) { bar.Close(); bar = null; }
        if (frame != null) { frame.Close(); frame = null; }
        if (camera != null) { camera.Close(); camera = null; }
    }

    void BackToMain() {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        RefreshRecents();
        UpdateUi();
    }

    void StopRecording() {
        var s = session;
        if (s == null) return;
        session = null;
        CloseOverlays();
        bool ok = s.Stop();
        BackToMain();
        if (!ok) { Error("The recording stopped unexpectedly.\n\n" + s.ErrorTail()); return; }
        int tracks = (s.HasMic ? 1 : 0) + (s.HasSys ? 1 : 0);
        string dst = s.RawFile.Replace(" (uncompressed).mkv", ".mp4");
        StartEncode(s.RawFile, dst, Encode.Best, tracks, s.HasMic, s.Pauses, s.Mutes, s.Fps,
                    "Compressing recording", On("ChkKeep") ? null : s.RawFile, true);
        if (job != null && tracks > 0 && On("ChkSubs")) {
            string style = SubStyle(), model = SubModel(), lang = SubLang();
            job.Then = delegate { MakeSubtitles(dst, style, model, lang); };
        }
    }

    void DiscardRecording() {
        if (session == null) return;     // the control bar already asked for a second click to confirm
        var s = session;
        session = null;
        CloseOverlays();
        s.Discard();
        BackToMain();
    }

    // ---- compression / export ------------------------------------------------------------------

    void StartEncode(string src, string dst, string quality, int tracks, bool micFirst,
                     List<double[]> pauses, List<double[]> mutes, int fps, string verb, string deleteWhenDone, bool freshCapture = false) {
        string progress = Path.Combine(Path.GetTempPath(), "mmwsr-enc-" + Guid.NewGuid() + ".txt");
        int found;
        double srcDur = FF.Probe(src, out found);
        if (tracks < 0) tracks = Math.Min(found, 2);
        double startAt = freshCapture ? FF.LeadIn(src) : 0;
        double outDur;
        string args = Encode.Args(src, dst, quality, tracks, micFirst, pauses, mutes, fps, srcDur, startAt, progress, out outDur);
        RunJob(args, null, dst, outDur, progress, verb, deleteWhenDone);
    }

    // Runs one FFmpeg step in the background with progress shown in the recordings card.
    void RunJob(string args, string workDir, string output, double duration, string progressFile, string verb, string deleteWhenDone) {
        try { job = FF.Start(args, workDir); }
        catch (Exception e) { Error("Could not start FFmpeg:\n" + e.Message); return; }
        job.File = output;
        job.Duration = duration;
        job.ProgressFile = progressFile;
        job.Verb = verb;
        job.DeleteWhenDone = deleteWhenDone;
        F<Border>("BusyCard").Visibility = Visibility.Visible;
        F<TextBlock>("BusyText").Text = verb + "...";
        F<TextBlock>("BusyPct").Text = "";
        F<ProgressBar>("BusyBar").Value = 0;
        RefreshRecents();
        UpdateUi();
    }

    void UpdateJob() {
        var j = job;
        if (j.Proc.HasExited) {
            job = null;
            F<Border>("BusyCard").Visibility = Visibility.Collapsed;
            try { File.Delete(j.ProgressFile); } catch { }
            if (j.Proc.ExitCode == 0) {
                if (j.DeleteWhenDone != null) try { File.Delete(j.DeleteWhenDone); } catch { }
                if (j.Then != null) {
                    try { j.Then(); } catch (Exception e) { Error(e.Message); }
                    if (job != null) { RefreshRecents(); UpdateUi(); return; }
                }
            } else {
                try { File.Delete(j.File); } catch { }
                Error(j.Verb + " failed." + (j.DeleteWhenDone != null ? " The uncompressed recording has been kept." : "") +
                      (j.Verb.Contains("ubtitle") ? " The recording itself is fine." : "") + "\n\n" + j.ErrorTail());
            }
            RefreshRecents();
            UpdateUi();
            return;
        }
        double sec = j.ProgressSeconds();
        if (sec >= 0 && j.Duration > 0) {
            int pct = Math.Max(0, Math.Min(100, (int)(sec / j.Duration * 100)));
            F<ProgressBar>("BusyBar").Value = pct;
            F<TextBlock>("BusyPct").Text = pct + "%";
        }
    }

    // ---- subtitles ---------------------------------------------------------------------------

    // Downloads the speech model if needed (asking first). Returns true when it is available.
    async Task<bool> EnsureModel(string quality) {
        if (Subtitles.HasModel(quality)) return true;
        if (downloadingModel) return false;
        var r = MessageBox.Show(this, "Subtitles need a one-time download of the speech model (" + Subtitles.ModelMB(quality) +
            " MB). It runs on your PC, so your recordings are never uploaded.\n\nDownload it now?", Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return false;
        downloadingModel = true;
        F<Border>("BusyCard").Visibility = Visibility.Visible;
        F<TextBlock>("BusyText").Text = "Downloading speech model (" + Subtitles.ModelMB(quality) + " MB)...";
        F<TextBlock>("BusyPct").Text = "";
        F<ProgressBar>("BusyBar").Value = 0;
        UpdateUi();
        try {
            await Subtitles.Download(quality, p => Dispatcher.BeginInvoke(new Action(() => {
                F<ProgressBar>("BusyBar").Value = p;
                F<TextBlock>("BusyPct").Text = p + "%";
            })));
            return true;
        } catch (Exception e) {
            Error("The speech model couldn't be downloaded:\n" + e.Message);
            return false;
        } finally {
            downloadingModel = false;
            if (job == null) F<Border>("BusyCard").Visibility = Visibility.Collapsed;
            UpdateUi();
        }
    }

    // Transcribes a recording, saves a .srt next to it, then embeds or burns in the subtitles.
    async void MakeSubtitles(string video, string style, string quality, string language) {
        if (job != null) { Error("Please wait until the current " + job.Verb.ToLowerInvariant() + " finishes."); return; }
        if (!await EnsureModel(quality)) return;
        int audio;
        double dur = FF.Probe(video, out audio);
        if (audio == 0) { Error("This recording has no sound, so there is nothing to transcribe."); return; }

        string dir = Path.GetDirectoryName(video);
        string tag = "mmwsr-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string tmpSrtName = tag + ".srt", tmpSrt = Path.Combine(dir, tmpSrtName);
        string finalSrt = Path.ChangeExtension(video, ".srt");
        string tmpVideo = Path.Combine(dir, tag + ".mp4");
        string progress = Path.Combine(Path.GetTempPath(), tag + ".txt");

        RunJob(Subtitles.TranscribeArgs(video, tmpSrtName, quality, language, progress), dir, tmpSrt, dur, progress, "Creating subtitles", null);
        if (job == null) return;
        job.Then = delegate {
            if (!Subtitles.Tidy(tmpSrt)) {
                try { File.Delete(tmpSrt); } catch { }
                MessageBox.Show(this, "No speech was found in this recording, so no subtitles were made.", Program.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string args = style == Subtitles.Burn
                ? Subtitles.BurnArgs(video, tmpSrtName, tmpVideo, progress)
                : Subtitles.EmbedArgs(video, tmpSrt, tmpVideo, progress);
            RunJob(args, dir, tmpVideo, dur, progress, style == Subtitles.Burn ? "Adding subtitles to the video" : "Adding subtitles", null);
            if (job == null) return;
            job.Then = delegate {
                try { File.Copy(tmpSrt, finalSrt, true); File.Delete(tmpSrt); } catch { }
                try {
                    File.Delete(video);
                    File.Move(tmpVideo, video);
                } catch {
                    // The original is probably open in a player; keep the subtitled copy next to it.
                    string alt = Path.Combine(dir, Path.GetFileNameWithoutExtension(video) + " (subtitles).mp4");
                    try { File.Move(tmpVideo, alt); } catch { }
                }
            };
        };
    }

    void Tick() {
        if (session != null) {
            if (session.Exited) {
                var s = session;
                session = null;
                CloseOverlays();
                s.Stop();
                BackToMain();
                Error("The recording stopped unexpectedly.\n\n" + s.ErrorTail());
                return;
            }
            if (bar != null) bar.Update(session.Elapsed, session.Paused, session.MicMuted);
            var t = session.Elapsed;
            tray.Text = (session.Paused ? "Paused " : "Recording ") + t.ToString(@"mm\:ss") + " - click to stop";
        }
        if (job != null) UpdateJob();
    }

    // ---- recordings list -----------------------------------------------------------------------

    void RefreshRecents() {
        var list = F<StackPanel>("RecentList");
        list.Children.Clear();
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (job != null) { skip.Add(job.File); if (job.DeleteWhenDone != null) skip.Add(job.DeleteWhenDone); }
        if (session != null) skip.Add(session.RawFile);
        List<FileInfo> files = new List<FileInfo>();
        try {
            var dir = new DirectoryInfo(settings.Folder);
            if (dir.Exists)
                files = dir.GetFiles("*.mp4").Concat(dir.GetFiles("*.mkv"))
                           .Where(f => !skip.Contains(f.FullName) && !f.Name.StartsWith("mmwsr-"))
                           .OrderByDescending(f => f.LastWriteTime).Take(3).ToList();
        } catch { }
        foreach (var f in files) list.Children.Add(MakeRow(f));
        F<TextBlock>("EmptyText").Visibility = files.Count == 0 && job == null ? Visibility.Visible : Visibility.Collapsed;
        UpdateUi();
    }

    static string Pretty(FileInfo f) {
        string name = Path.GetFileNameWithoutExtension(f.Name);
        DateTime d;
        string stamp = name.Replace("Recording ", "").Replace(" (uncompressed)", "");
        if (DateTime.TryParseExact(stamp, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) {
            string day = d.Date == DateTime.Today ? "Today" : d.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : d.ToString("d MMM yyyy");
            return day + ", " + d.ToString("HH:mm");
        }
        return name;
    }

    static string Size(long bytes) {
        if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
        if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("0.0") + " MB";
        return Math.Max(1, bytes / 1024) + " KB";
    }

    UIElement MakeRow(FileInfo f) {
        bool lossless = f.Extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);
        var row = new Button { Style = (Style)FindResource("Row"), Margin = new Thickness(0, 0, 0, 6), ToolTip = f.FullName };
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(9), Background = (Brush)FindResource("Card2"),
            Child = new TextBlock {
                Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = 14,
                Foreground = (Brush)FindResource("Accent"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        g.Children.Add(icon);

        var text = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = Pretty(f), FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        text.Children.Add(new TextBlock {
            Text = Size(f.Length) + "  ·  " + (lossless ? "Uncompressed MKV" : "MP4") +
                   (File.Exists(Path.ChangeExtension(f.FullName, ".srt")) ? "  ·  Subtitles" : ""),
            FontSize = 11.5, Foreground = (Brush)FindResource("Muted"), Margin = new Thickness(0, 1, 0, 0)
        });
        Grid.SetColumn(text, 1);
        g.Children.Add(text);

        var more = new Button { Style = (Style)FindResource("IconButton"), Content = "", ToolTip = "More" };
        System.Windows.Automation.AutomationProperties.SetName(more, "More options");
        Grid.SetColumn(more, 2);
        g.Children.Add(more);
        row.Content = g;

        row.Click += delegate { Play(f.FullName); };
        more.Click += delegate(object s, RoutedEventArgs e) {
            e.Handled = true;     // don't let the click reach the row (which would start playback)
            var menu = new ContextMenu();
            menu.Items.Add(Item("Show in folder", "", delegate { Process.Start("explorer.exe", "/select,\"" + f.FullName + "\""); }));
            if (!lossless) menu.Items.Add(Item(File.Exists(Path.ChangeExtension(f.FullName, ".srt")) ? "Redo subtitles" : "Create subtitles", "", delegate { MakeSubtitles(f.FullName, SubStyle(), SubModel(), SubLang()); }));
            menu.Items.Add(Item("Export as high-quality MP4", "", delegate { Export(f.FullName, Encode.Best); }));
            menu.Items.Add(Item("Export as extra-small MP4", "", delegate { Export(f.FullName, Encode.Small); }));
            if (lossless) menu.Items.Add(Item("Export as lossless MKV", "", delegate { Export(f.FullName, Encode.Lossless); }));
            menu.Items.Add(Item("Move to Recycle Bin", "", delegate { Recycle(f.FullName); }));
            menu.PlacementTarget = more;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        };
        return row;
    }

    static MenuItem Item(string text, string glyph, RoutedEventHandler click) {
        var m = new MenuItem { Header = text, Tag = glyph };
        m.Click += click;
        return m;
    }

    void Play(string file) {
        try {
            // The built-in Windows player can't decode lossless RGB H.264, so use ffplay for those.
            if (file.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) && FF.FFplay != null) {
                var psi = new ProcessStartInfo(FF.FFplay, "-hide_banner -autoexit -window_title \"Playback\" " + FF.Q(file));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            } else {
                Process.Start(file);
            }
        } catch (Exception e) {
            Error("Could not play the file:\n" + e.Message);
        }
    }

    void Export(string src, string quality) {
        if (job != null) { Error("Please wait until the current " + job.Verb.ToLowerInvariant() + " finishes."); return; }
        string ext = quality == Encode.Lossless ? "mkv" : "mp4";
        var dlg = new Microsoft.Win32.SaveFileDialog();
        dlg.Title = "Export as";
        dlg.Filter = ext.ToUpperInvariant() + " video (*." + ext + ")|*." + ext;
        dlg.InitialDirectory = Path.GetDirectoryName(src);
        dlg.FileName = Path.GetFileNameWithoutExtension(src).Replace(" (uncompressed)", "") +
                       (quality == Encode.Small ? " (small)" : " (export)") + "." + ext;
        if (dlg.ShowDialog(this) != true) return;
        if (string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(src), StringComparison.OrdinalIgnoreCase)) {
            Error("Pick a different file name than the original.");
            return;
        }
        StartEncode(src, dlg.FileName, quality, -1, false, null, null, 30, "Exporting", null);
    }

    void Recycle(string file) {
        var r = MessageBox.Show(this, "Move \"" + Path.GetFileName(file) + "\" to the Recycle Bin?", Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            string srt = Path.ChangeExtension(file, ".srt");
            if (File.Exists(srt))
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(srt, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        } catch (Exception e) { Error(e.Message); }
        RefreshRecents();
    }

    void ChangeFolder() {
        using (var fb = new System.Windows.Forms.FolderBrowserDialog()) {
            fb.Description = "Where should recordings be saved?";
            fb.SelectedPath = settings.Folder;
            if (fb.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                settings.Folder = fb.SelectedPath;
                SaveSettings();
                RefreshRecents();
            }
        }
    }

    void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
        if (session != null) {
            // Keep what was recorded; it stays as an uncompressed file the user can export later.
            var s = session;
            session = null;
            CloseOverlays();
            s.Stop();
        }
        if (job != null && !job.Proc.HasExited) {
            string what = job.Verb.StartsWith("Compress") ? "The recording is still being compressed" : "An export is still running";
            var r = MessageBox.Show(this, what + ". Cancel it and quit?\n\nThe original file will be kept.", Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) { e.Cancel = true; return; }
            try { job.Proc.Kill(); job.Proc.WaitForExit(3000); File.Delete(job.File); } catch { }
        }
        SaveSettings();
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
    }
}

}
