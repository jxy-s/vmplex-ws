﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Drawing;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VMPlex
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            SetValue(FontFamilyProperty, System.Windows.SystemFonts.MessageFontFamily);
            SetValue(FontSizeProperty, System.Windows.SystemFonts.MessageFontSize);

            System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(new Uri("/Resources/VMPlex.ico", UriKind.Relative));
            Icon icon = new Icon(info.Stream, 16, 16);
            TbIcon.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, object e)
        {
            var s = UserSettings.Instance.Settings;
            Width = s.MainWindow.Width;
            Height = s.MainWindow.Height;
            Top = s.MainWindow.Top;
            Left = s.MainWindow.Left;
            if (s.MainWindow.State != WindowState.Minimized)
            {
                WindowState = s.MainWindow.State;
            }
        }

        private void MainWindow_Closing(object sender, object e)
        {
            UserSettings.Instance.Mutate(s =>
            {
                s.MainWindow.Width = Width;
                s.MainWindow.Height = Height;
                s.MainWindow.Top = Top;
                s.MainWindow.Left = Left;
                s.MainWindow.State = WindowState;
                return s;
            });
        }
    }
}
