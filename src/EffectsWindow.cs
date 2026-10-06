// "Camera effects" window: live camera preview with background (none / blur / scene) and colour filter choices.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MakeMyWebRecorder {

class EffectsWindow : Window {
    const string Xaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Margin='14' Background='{StaticResource Bg}' CornerRadius='16' BorderBrush='{StaticResource Line}' BorderThickness='1'>
  <Border.Effect><DropShadowEffect BlurRadius='26' ShadowDepth='4' Opacity='0.45'/></Border.Effect>
  <StackPanel Margin='20,14,20,20' Width='384'>
    <Grid x:Name='TitleBar' Background='Transparent'>
      <TextBlock Text='Camera effects' FontSize='15' FontWeight='SemiBold' VerticalAlignment='Center'/>
      <Button x:Name='BtnClose' Style='{StaticResource IconButton}' Content='&#xE8BB;' FontSize='10' HorizontalAlignment='Right'
              ToolTip='Close' AutomationProperties.Name='Close'/>
    </Grid>
    <Border x:Name='PreviewHost' Width='230' Height='230' Margin='0,12,0,4' HorizontalAlignment='Center'/>
    <TextBlock x:Name='FxStatus' Style='{StaticResource Hint}' HorizontalAlignment='Center' TextAlignment='Center' Margin='0,4,0,0'/>

    <TextBlock Text='BACKGROUND' Style='{StaticResource Caption}' Margin='0,16,0,0'/>
    <Border Background='{StaticResource Card}' CornerRadius='10' Padding='3' Margin='0,8,0,0'>
      <UniformGrid Columns='3'>
        <RadioButton x:Name='BgNone' GroupName='bg' Style='{StaticResource Pill}' Content='None'/>
        <RadioButton x:Name='BgBlur' GroupName='bg' Style='{StaticResource Pill}' Content='Blur'/>
        <RadioButton x:Name='BgScene' GroupName='bg' Style='{StaticResource Pill}' Content='Scene'/>
      </UniformGrid>
    </Border>
    <WrapPanel x:Name='SceneList' Margin='0,12,-8,0'/>

    <TextBlock Text='FILTER' Style='{StaticResource Caption}' Margin='0,10,0,0'/>
    <Border Background='{StaticResource Card}' CornerRadius='10' Padding='3' Margin='0,8,0,0'>
      <UniformGrid Columns='5'>
        <RadioButton x:Name='FxNone' GroupName='fx' Style='{StaticResource Pill}' Content='None'/>
        <RadioButton x:Name='FxWarm' GroupName='fx' Style='{StaticResource Pill}' Content='Warm'/>
        <RadioButton x:Name='FxCool' GroupName='fx' Style='{StaticResource Pill}' Content='Cool'/>
        <RadioButton x:Name='FxMono' GroupName='fx' Style='{StaticResource Pill}' Content='B&amp;W'/>
        <RadioButton x:Name='FxBright' GroupName='fx' Style='{StaticResource Pill}' Content='Bright'/>
      </UniformGrid>
    </Border>

    <Button x:Name='BtnDone' Style='{StaticResource PrimaryButton}' Height='42' FontSize='14' Margin='0,22,0,0' Content='Done'/>
  </StackPanel>
</Border>";

    readonly FrameworkElement root;
    readonly CameraEffects fx;
    readonly CameraView preview;
    readonly Dictionary<string, RadioButton> sceneTiles = new Dictionary<string, RadioButton>();
    bool downloading;

    T F<T>(string name) { return (T)root.FindName(name); }

    public EffectsWindow(Window owner, string device, CameraEffects effects, double scale) {
        Owner = owner;
        fx = effects;
        Title = "Camera effects";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        FontFamily = (FontFamily)FindResource("UiFont");
        Foreground = (Brush)FindResource("Text");
        UseLayoutRounding = true;

        root = (FrameworkElement)XamlReader.Parse(Xaml);
        Content = root;
        F<Grid>("TitleBar").MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
        F<Button>("BtnClose").Click += delegate { Close(); };
        F<Button>("BtnDone").Click += delegate { Close(); };
        KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };

        preview = new CameraView(device, 230, scale, fx);
        F<Border>("PreviewHost").Child = preview;
        preview.Ready.ContinueWith(t => {
            if (t.Result != null) Dispatcher.BeginInvoke(new Action(() => F<TextBlock>("FxStatus").Text = t.Result));
        });

        // Scene tiles: built-in scenes plus "your image".
        foreach (var scene in CameraEffects.Scenes) AddTile(scene, Thumbnail(scene, null));
        AddTile("custom", fx.CustomImage != "" && File.Exists(fx.CustomImage) ? Thumbnail("custom", fx.CustomImage) : null);

        F<RadioButton>(fx.Background == "blur" ? "BgBlur" : fx.Background == "scene" ? "BgScene" : "BgNone").IsChecked = true;
        F<RadioButton>("Fx" + Cap(fx.Filter)).IsChecked = true;
        RadioButton current;
        if (sceneTiles.TryGetValue(fx.Scene, out current)) current.IsChecked = true;
        UpdateScenes();

        F<RadioButton>("BgNone").Checked += delegate { SetBackground("none"); };
        F<RadioButton>("BgBlur").Checked += delegate { SetBackground("blur"); };
        F<RadioButton>("BgScene").Checked += delegate { SetBackground("scene"); };
        foreach (var f in CameraEffects.Filters) {
            string name = f;
            F<RadioButton>("Fx" + Cap(f)).Checked += delegate { fx.Filter = name; };
        }
        if (fx.NeedsModel) EnsureModel();
        Closed += delegate { preview.Stop(); };
    }

    static string Cap(string s) { return char.ToUpperInvariant(s[0]) + s.Substring(1); }

    void SetBackground(string bg) {
        fx.Background = bg;
        UpdateScenes();
        if (fx.NeedsModel) EnsureModel();
    }

    void UpdateScenes() {
        F<WrapPanel>("SceneList").Visibility = fx.Background == "scene" ? Visibility.Visible : Visibility.Collapsed;
    }

    async void EnsureModel() {
        if (SegmentationModel.Available || downloading) return;
        downloading = true;
        var status = F<TextBlock>("FxStatus");
        status.Text = "Preparing background effects (one-time download, under 1 MB)...";
        try {
            await SegmentationModel.Download();
            status.Text = "";
        } catch (Exception e) {
            status.Text = "Couldn't download the background effect: " + e.Message;
        } finally {
            downloading = false;
        }
    }

    void AddTile(string scene, ImageSource image) {
        var tile = new RadioButton { Style = (Style)FindResource("Tile"), GroupName = "scene", Tag = SceneImages.Title(scene) };
        System.Windows.Automation.AutomationProperties.SetName(tile, SceneImages.Title(scene));
        if (image != null) tile.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        else {
            tile.Background = (Brush)FindResource("Card2");
            tile.Content = new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = 18, Foreground = (Brush)FindResource("Muted") };
        }
        string name = scene;
        tile.Checked += delegate {
            if (name == "custom" && (fx.CustomImage == "" || !File.Exists(fx.CustomImage)) && !PickImage()) {
                RadioButton prev;
                if (sceneTiles.TryGetValue(fx.Scene, out prev) && prev != tile) prev.IsChecked = true; else tile.IsChecked = false;
                return;
            }
            fx.Scene = name;
        };
        // Clicking "Your image" again lets you choose a different picture.
        if (scene == "custom")
            tile.PreviewMouseLeftButtonUp += delegate { if (tile.IsChecked == true) PickImage(); };
        sceneTiles[scene] = tile;
        F<WrapPanel>("SceneList").Children.Add(tile);
    }

    bool PickImage() {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        dlg.Title = "Choose a background image";
        dlg.Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
        if (dlg.ShowDialog(this) != true) return false;
        fx.CustomImage = dlg.FileName;
        fx.Scene = "custom";
        var tile = sceneTiles["custom"];
        tile.Content = null;
        tile.Background = new ImageBrush(Thumbnail("custom", dlg.FileName)) { Stretch = Stretch.UniformToFill };
        return true;
    }

    static ImageSource Thumbnail(string scene, string image) {
        using (var bmp = SceneImages.Render(scene, image, 120)) {
            var h = bmp.GetHbitmap();
            try { return Imaging.CreateBitmapSourceFromHBitmap(h, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); }
            finally { DeleteObject(h); }
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
}

}
