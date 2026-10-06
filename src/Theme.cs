// Dark, modern look shared by every window: colours, fonts and control templates.

using System.Windows;
using System.Windows.Markup;

namespace MakeMyWebRecorder {

static class Theme {
    public static ResourceDictionary Load() { return (ResourceDictionary)XamlReader.Parse(Xaml); }

    // Segoe Fluent Icons (Windows 11) with Segoe MDL2 Assets (Windows 10) as fallback.
    const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <SolidColorBrush x:Key='Bg' Color='#101217'/>
  <SolidColorBrush x:Key='Card' Color='#181B22'/>
  <SolidColorBrush x:Key='Card2' Color='#21252E'/>
  <SolidColorBrush x:Key='Line' Color='#2A2F3A'/>
  <SolidColorBrush x:Key='Text' Color='#EEF0F4'/>
  <SolidColorBrush x:Key='Muted' Color='#8E96A5'/>
  <SolidColorBrush x:Key='Accent' Color='#FF3D5E'/>
  <SolidColorBrush x:Key='AccentHover' Color='#FF5A77'/>
  <SolidColorBrush x:Key='Amber' Color='#F5B83D'/>
  <FontFamily x:Key='Icons'>Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>
  <FontFamily x:Key='UiFont'>Segoe UI Variable Text, Segoe UI</FontFamily>

  <Style x:Key='Caption' TargetType='TextBlock'>
    <Setter Property='FontSize' Value='11'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
  </Style>
  <Style x:Key='Hint' TargetType='TextBlock'>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='TextWrapping' Value='Wrap'/>
  </Style>
  <Style x:Key='Icon' TargetType='TextBlock'>
    <Setter Property='FontFamily' Value='{StaticResource Icons}'/>
    <Setter Property='FontSize' Value='16'/>
    <Setter Property='VerticalAlignment' Value='Center'/>
  </Style>

  <Style TargetType='ToolTip'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToolTip'>
          <Border Background='#262A33' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='6' Padding='8,5'>
            <ContentPresenter TextElement.FontSize='12'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='IconButton' TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Width' Value='34'/>
    <Setter Property='Height' Value='34'/>
    <Setter Property='FontFamily' Value='{StaticResource Icons}'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='9'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#1AFFFFFF'/>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#2AFFFFFF'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.35'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='PrimaryButton' TargetType='Button'>
    <Setter Property='Foreground' Value='White'/>
    <Setter Property='Background' Value='{StaticResource Accent}'/>
    <Setter Property='Height' Value='50'/>
    <Setter Property='FontSize' Value='15'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='12'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{StaticResource AccentHover}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='b' Property='Opacity' Value='0.85'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='b' Property='Background' Value='{StaticResource Card2}'/>
              <Setter Property='Foreground' Value='{StaticResource Muted}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='LinkButton' TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='Transparent' CornerRadius='6' Padding='8,4'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#14FFFFFF'/>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Big segmented choice: icon (Tag) above label (Content). -->
  <Style x:Key='Segment' TargetType='RadioButton'>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RadioButton'>
          <Border x:Name='b' Background='Transparent' CornerRadius='9' Padding='6,9' BorderThickness='1' BorderBrush='Transparent'>
            <StackPanel HorizontalAlignment='Center'>
              <TextBlock Text='{TemplateBinding Tag}' FontFamily='{StaticResource Icons}' FontSize='18' HorizontalAlignment='Center'/>
              <ContentPresenter HorizontalAlignment='Center' Margin='0,5,0,0' TextElement.FontSize='12.5'/>
            </StackPanel>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{StaticResource Card2}'/>
              <Setter TargetName='b' Property='BorderBrush' Value='#3A404D'/>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Small segmented choice (e.g. 30 / 60 fps). -->
  <Style x:Key='Pill' TargetType='RadioButton'>
    <Setter Property='Foreground' Value='{StaticResource Muted}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RadioButton'>
          <Border x:Name='b' Background='Transparent' CornerRadius='7' Padding='12,4'>
            <ContentPresenter HorizontalAlignment='Center' TextElement.FontSize='12.5'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#343A47'/>
              <Setter Property='Foreground' Value='{StaticResource Text}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Toggle switch: label on the left, switch on the right. -->
  <Style x:Key='Switch' TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <Grid Background='Transparent' MinHeight='40'>
            <Grid.ColumnDefinitions>
              <ColumnDefinition/>
              <ColumnDefinition Width='Auto'/>
            </Grid.ColumnDefinitions>
            <ContentPresenter VerticalAlignment='Center' TextElement.FontSize='13.5'/>
            <Border x:Name='track' Grid.Column='1' Width='38' Height='22' CornerRadius='11' Background='#343A47' VerticalAlignment='Center'>
              <Ellipse x:Name='knob' Width='16' Height='16' Fill='#C9CED8' HorizontalAlignment='Left' Margin='3,0,0,0'/>
            </Border>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='track' Property='Background' Value='{StaticResource Accent}'/>
              <Setter TargetName='knob' Property='Fill' Value='White'/>
              <Setter TargetName='knob' Property='Margin' Value='19,0,0,0'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.4'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Scene thumbnail: picture as Background, title as Tag. -->
  <Style x:Key='Tile' TargetType='RadioButton'>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='RadioButton'>
          <StackPanel Width='66' Margin='0,0,8,8'>
            <Border x:Name='b' Width='62' Height='62' CornerRadius='11' BorderThickness='2' BorderBrush='Transparent' Background='{TemplateBinding Background}'>
              <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
            </Border>
            <TextBlock Text='{TemplateBinding Tag}' FontSize='10.5' Foreground='{StaticResource Muted}' HorizontalAlignment='Center' TextTrimming='CharacterEllipsis' Margin='0,4,0,0'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='BorderBrush' Value='#5A6273'/>
            </Trigger>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='b' Property='BorderBrush' Value='{StaticResource Accent}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='6'/>
    <Setter Property='MinWidth' Value='6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border Background='#3A404D' CornerRadius='3'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ComboBox'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Height' Value='40'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBox'>
          <Grid>
            <ToggleButton Focusable='False' ClickMode='Press' Cursor='Hand'
                          IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='ToggleButton'>
                  <Border x:Name='bd' Background='{StaticResource Card2}' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='10'>
                    <TextBlock Text='&#xE70D;' FontFamily='{StaticResource Icons}' FontSize='10' Foreground='{StaticResource Muted}'
                               HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,13,0'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                      <Setter TargetName='bd' Property='BorderBrush' Value='#454C5B'/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}'
                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                              Margin='13,0,34,0' VerticalAlignment='Center' HorizontalAlignment='Stretch'/>
            <Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'>
              <Border Background='#1F232B' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='10' Margin='0,4,0,0' Padding='4'
                      MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
                      MaxWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='330'>
                <ScrollViewer VerticalScrollBarVisibility='Auto'>
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.45'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ComboBoxItem'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBoxItem'>
          <Border x:Name='b' Background='Transparent' CornerRadius='7' Padding='10,7'>
            <ContentPresenter HorizontalAlignment='Stretch'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#2C313C'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#33FF3D5E'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ContextMenu'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ContextMenu'>
          <Border Background='#1F232B' BorderBrush='{StaticResource Line}' BorderThickness='1' CornerRadius='10' Padding='4'>
            <StackPanel IsItemsHost='True'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='MenuItem'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='MenuItem'>
          <Border x:Name='b' Background='Transparent' CornerRadius='7' Padding='10,7' MinWidth='210'>
            <StackPanel Orientation='Horizontal'>
              <TextBlock Text='{TemplateBinding Tag}' FontFamily='{StaticResource Icons}' FontSize='14' Width='26' VerticalAlignment='Center' Foreground='{StaticResource Muted}'/>
              <ContentPresenter ContentSource='Header' VerticalAlignment='Center' TextElement.FontSize='13'/>
            </StackPanel>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#2C313C'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='Row' TargetType='Button'>
    <Setter Property='Foreground' Value='{StaticResource Text}'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{StaticResource Card}' CornerRadius='11' Padding='10,8'>
            <ContentPresenter HorizontalAlignment='Stretch'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{StaticResource Card2}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ProgressBar'>
    <Setter Property='Height' Value='6'/>
    <Setter Property='Foreground' Value='{StaticResource Accent}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ProgressBar'>
          <Border Background='#2C313C' CornerRadius='3' ClipToBounds='True'>
            <Grid>
              <Border x:Name='PART_Track'/>
              <Border x:Name='PART_Indicator' Background='{TemplateBinding Foreground}' CornerRadius='3' HorizontalAlignment='Left'/>
            </Grid>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
}

}
