/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XTMF.Gui.UserControls
{
    public class HintedTextBox : TextBox
    {
        private string hintText;
        private FormattedText HintTextImage;
        private static ContextMenu _LocalContextMenu;

        static HintedTextBox()
        {
            var contextMenu = new ContextMenu();
            // copy
            contextMenu.Items.Add(new MenuItem()
            {
                Header = "Copy",
                Command = ApplicationCommands.Copy,
                Icon = new Image()
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/XTMF.Gui;component/Images/CopyHS.png")),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Width = 20,
                    Height = 20
                }
            });
            // cut
            contextMenu.Items.Add(new MenuItem()
            {
                Header = "Cut",
                Command = ApplicationCommands.Cut,
                Icon = new Image()
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/XTMF.Gui;component/Images/CutHS.png"))
                    ,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Width = 20,
                    Height = 20
                }
            });
            // paste
            contextMenu.Items.Add(new MenuItem()
            {
                Header = "Paste",
                Command = ApplicationCommands.Paste,
                Icon = new Image()
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/XTMF.Gui;component/Images/PasteHS.png")),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Width = 20,
                    Height = 20
                }
            });
            _LocalContextMenu = contextMenu;
        }

        public HintedTextBox()
        {
            this.ClipToBounds = true;
            this.ContextMenu = _LocalContextMenu;
        }

        public string HintText
        {
            get
            {
                return this.hintText;
            }

            set
            {
                hintText = value;
                if (!String.IsNullOrEmpty(hintText))
                {
                    this.Background = Brushes.Transparent;
                }
                RebuildFont();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (this.ClipToBounds)
            {
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, this.ActualWidth, this.ActualHeight)));
            }
            dc.DrawRectangle(Brushes.White, new Pen(), new Rect(0, 0, this.ActualWidth, this.ActualHeight));
            base.OnRender(dc);
            if (this.Text == String.Empty && this.HintTextImage != null)
            {
                dc.DrawText(this.HintTextImage, new Point(4, (this.ActualHeight - this.HintTextImage.Height) / 2));
            }
            if (this.ClipToBounds)
            {
                // pop the clip that we added
                dc.Pop();
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            this.InvalidateVisual();
        }

        private void RebuildFont()
        {
            if (this.hintText == null)
            {
                this.HintTextImage = null;
            }
            else
            {
                this.HintTextImage = new FormattedText(this.hintText, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch), this.FontSize, Brushes.Gray);
                int constantBorder = 8;
                if (this.ActualWidth > 0)
                {
                    if (this.HintTextImage.Width + constantBorder > this.ActualWidth)
                    {
                        this.Width = this.HintTextImage.Width + constantBorder;
                    }
                }
            }
            this.InvalidateVisual();
        }
    }
}