using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XTMF.Gui.CustomProperties
{
    public static class VisualAnimationProperty
    {
           public static readonly DependencyProperty IsRenderVisualAnimationProperty = DependencyProperty.RegisterAttached(
          "IsRenderVisualAnimationProperty",
          typeof(Boolean),
          typeof(PopupBox),
          new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender)
        );
        public static void SetIsRenderVisualAnimation(UIElement element, Boolean value)
        {
            element.SetValue(IsRenderVisualAnimationProperty, value);

            if (value)
            {
                element.IsVisibleChanged += Element_IsVisibleChanged;

            }
            else
            {
                element.IsVisibleChanged -= Element_IsVisibleChanged;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Element_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

            //Adds a "breathing" / scale animation to the element when it is brought into view.
            //Repeats 3 times.
            var element = sender as PopupBox;
            if (element.IsVisible == true)
            {
                ScaleTransform scaleTransform = new(1.0, 1.0, element.Width / 2, element.Height / 2);
                element.RenderTransform = scaleTransform;
                DoubleAnimation animation = new();
                animation.From = 1.4;
                animation.To = 1.0;
                animation.Duration = new Duration(TimeSpan.FromSeconds(0.3));
                animation.AutoReverse = false;
                animation.RepeatBehavior = new RepeatBehavior(1);
                element.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
                element.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Boolean GetIsRenderVisualAnimation(UIElement element)
        {
            return (Boolean)element.GetValue(IsRenderVisualAnimationProperty);
        }
    }
}
