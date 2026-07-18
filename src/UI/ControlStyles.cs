using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace DeskMate.UI
{
    /// <summary>Theme-matched WPF control styles (ComboBox, CheckBox, ScrollBar, Slider)
    /// built from XAML templates with the current palette colors baked in.
    /// Styles are rebuilt lazily after every Theme.Apply.</summary>
    public static class ControlStyles
    {
        static Style _combo, _check, _scroll, _slider;

        public static void Invalidate()
        {
            _combo = _check = _scroll = _slider = null;
        }

        static string Hex(Color c)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
        }

        static string HexOf(Brush b, string fallback)
        {
            var s = b as SolidColorBrush;
            return s != null ? Hex(s.Color) : fallback;
        }

        static Style Parse(string xaml)
        {
            return (Style)XamlReader.Parse(xaml);
        }

        static string Tokens(string xaml)
        {
            var accent = Hex(Theme.AccentColor);
            var accentSel = Hex(Color.FromArgb(0x36, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var pal = Theme.Palettes[Math.Max(0, Math.Min(Theme.Palettes.Length - 1, DeskMate.Core.Settings.I.ThemeIndex))];
            var popupC = pal.Light ? pal.CardTop : pal.CardBottom;
            var popupBg = Hex(Color.FromArgb(0xF6, popupC.R, popupC.G, popupC.B));
            string onAccent = Theme.IsLight ? "#FFFFFFFF" : "#FF000000";
            string thumb = Theme.IsLight ? "#50182430" : "#4CFFFFFF";
            return xaml
                .Replace("@ACCENT@", accent)
                .Replace("@ACCENTSEL@", accentSel)
                .Replace("@TEXT@", HexOf(Theme.Text, "#FFFFFFFF"))
                .Replace("@DIM@", HexOf(Theme.TextDim, "#B4FFFFFF"))
                .Replace("@BORDER@", HexOf(Theme.CardBorder, "#2AFFFFFF"))
                .Replace("@INPUT@", HexOf(Theme.InputBg, "#28000000"))
                .Replace("@HOVER@", HexOf(Theme.HoverBg, "#30FFFFFF"))
                .Replace("@POPUP@", popupBg)
                .Replace("@ONACCENT@", onAccent)
                .Replace("@THUMB@", thumb);
        }

        const string NS = "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                          "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'";

        // ---------------- ComboBox ----------------

        public static Style Combo()
        {
            if (_combo != null) return _combo;
            string xaml = @"
<Style " + NS + @" TargetType='ComboBox'>
  <Setter Property='SnapsToDevicePixels' Value='True'/>
  <Setter Property='Foreground' Value='@TEXT@'/>
  <Setter Property='MinHeight' Value='30'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBox'>
        <Grid>
          <ToggleButton x:Name='toggle' ClickMode='Press' Focusable='False'
                        IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
            <ToggleButton.Template>
              <ControlTemplate TargetType='ToggleButton'>
                <Border x:Name='bd' Background='@INPUT@' BorderBrush='@BORDER@' BorderThickness='1' CornerRadius='8'>
                  <Path Data='M 0 0 L 4 4 L 8 0' Stroke='@DIM@' StrokeThickness='1.5'
                        HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,1,10,0'/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property='IsMouseOver' Value='True'>
                    <Setter TargetName='bd' Property='Background' Value='@HOVER@'/>
                    <Setter TargetName='bd' Property='BorderBrush' Value='@ACCENT@'/>
                  </Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </ToggleButton.Template>
          </ToggleButton>
          <ContentPresenter Content='{TemplateBinding SelectionBoxItem}'
                            ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                            Margin='11,0,26,0' VerticalAlignment='Center' IsHitTestVisible='False'
                            TextElement.Foreground='@TEXT@'/>
          <Popup IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True'
                 Placement='Bottom' VerticalOffset='4' PopupAnimation='Fade'>
            <Border Background='@POPUP@' BorderBrush='@BORDER@' BorderThickness='1' CornerRadius='10'
                    MinWidth='{TemplateBinding ActualWidth}' MaxHeight='260' Padding='4' Margin='0,0,10,10'>
              <Border.Effect>
                <DropShadowEffect Color='Black' BlurRadius='12' ShadowDepth='3' Opacity='0.4' Direction='270'/>
              </Border.Effect>
              <ScrollViewer VerticalScrollBarVisibility='Auto'>
                <ItemsPresenter/>
              </ScrollViewer>
            </Border>
          </Popup>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
  <Setter Property='ItemContainerStyle'>
    <Setter.Value>
      <Style TargetType='ComboBoxItem'>
        <Setter Property='Foreground' Value='@TEXT@'/>
        <Setter Property='SnapsToDevicePixels' Value='True'/>
        <Setter Property='Template'>
          <Setter.Value>
            <ControlTemplate TargetType='ComboBoxItem'>
              <Border x:Name='bd' Background='Transparent' CornerRadius='7' Padding='9,6,9,6' Margin='1'>
                <ContentPresenter/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property='IsHighlighted' Value='True'>
                  <Setter TargetName='bd' Property='Background' Value='@HOVER@'/>
                </Trigger>
                <Trigger Property='IsSelected' Value='True'>
                  <Setter TargetName='bd' Property='Background' Value='@ACCENTSEL@'/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
          </Setter.Value>
        </Setter>
      </Style>
    </Setter.Value>
  </Setter>
</Style>";
            _combo = Parse(Tokens(xaml));
            return _combo;
        }

        // ---------------- CheckBox ----------------

        public static Style Check()
        {
            if (_check != null) return _check;
            string xaml = @"
<Style " + NS + @" TargetType='CheckBox'>
  <Setter Property='Foreground' Value='@TEXT@'/>
  <Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='CheckBox'>
        <StackPanel Orientation='Horizontal' Background='Transparent'>
          <Border x:Name='box' Width='17' Height='17' CornerRadius='5' Background='@INPUT@'
                  BorderBrush='@BORDER@' BorderThickness='1' VerticalAlignment='Center'>
            <TextBlock x:Name='mark' Text='&#xE73E;' FontFamily='Segoe MDL2 Assets' FontSize='10.5'
                       Foreground='@ONACCENT@' HorizontalAlignment='Center' VerticalAlignment='Center'
                       Visibility='Collapsed'/>
          </Border>
          <ContentPresenter Margin='8,0,0,0' VerticalAlignment='Center' RecognizesAccessKey='True'/>
        </StackPanel>
        <ControlTemplate.Triggers>
          <Trigger Property='IsChecked' Value='True'>
            <Setter TargetName='box' Property='Background' Value='@ACCENT@'/>
            <Setter TargetName='box' Property='BorderBrush' Value='@ACCENT@'/>
            <Setter TargetName='mark' Property='Visibility' Value='Visible'/>
          </Trigger>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='box' Property='BorderBrush' Value='@ACCENT@'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            _check = Parse(Tokens(xaml));
            return _check;
        }

        // ---------------- ScrollBar (thin, rounded) ----------------

        public static Style Scroll()
        {
            if (_scroll != null) return _scroll;
            string xaml = @"
<Style " + NS + @" TargetType='ScrollBar'>
  <Setter Property='Width' Value='7'/>
  <Setter Property='Background' Value='Transparent'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ScrollBar'>
        <Grid Background='Transparent'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border Background='@THUMB@' CornerRadius='3.5' Width='7'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
  <Style.Triggers>
    <Trigger Property='Orientation' Value='Horizontal'>
      <Setter Property='Height' Value='0'/>
      <Setter Property='Width' Value='Auto'/>
    </Trigger>
  </Style.Triggers>
</Style>";
            _scroll = Parse(Tokens(xaml));
            return _scroll;
        }

        // ---------------- Slider (thin track, round accent thumb) ----------------

        public static Style Slider()
        {
            if (_slider != null) return _slider;
            string xaml = @"
<Style " + NS + @" TargetType='Slider'>
  <Setter Property='Height' Value='24'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='Slider'>
        <Grid VerticalAlignment='Center'>
          <Border Height='4' CornerRadius='2' Background='@INPUT@' BorderBrush='@BORDER@' BorderThickness='0.5'/>
          <Track x:Name='PART_Track'>
            <Track.DecreaseRepeatButton>
              <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Height='4' CornerRadius='2' Background='@ACCENT@'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton>
              <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>
                <RepeatButton.Template>
                  <ControlTemplate TargetType='RepeatButton'>
                    <Border Background='Transparent' Height='24'/>
                  </ControlTemplate>
                </RepeatButton.Template>
              </RepeatButton>
            </Track.IncreaseRepeatButton>
            <Track.Thumb>
              <Thumb Focusable='False'>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border Width='15' Height='15' CornerRadius='7.5' Background='@ACCENT@'
                            BorderBrush='#40000000' BorderThickness='1'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </Grid>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";
            _slider = Parse(Tokens(xaml));
            return _slider;
        }
    }
}
