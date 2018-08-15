using System;
using System.Windows;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls;

namespace XTMF.Gui.Helpers
{
    internal class GridWidthAnimation : AnimationTimeline
    {
        public static readonly DependencyProperty FromProperty =
            DependencyProperty.Register("From", typeof(GridLength), typeof(GridLengthAnimation),
                new FrameworkPropertyMetadata(GridLength.Auto));


        public static readonly DependencyProperty ToProperty =
            DependencyProperty.Register("To", typeof(GridLength), typeof(GridLengthAnimation),
                new FrameworkPropertyMetadata(GridLength.Auto));

        public override Type TargetPropertyType => typeof(GridLength);


        public GridLength From

        {
            get => (GridLength) GetValue(FromProperty);

            set => SetValue(FromProperty, value);
        }


        public GridLength To

        {
            get => (GridLength) GetValue(ToProperty);

            set => SetValue(ToProperty, value);
        }


        protected override Freezable CreateInstanceCore()

        {
            return new GridLengthAnimation();
        }


        /// <summary>
        /// </summary>
        /// <param name="defaultOriginValue"></param>
        /// <param name="defaultDestinationValue"></param>
        /// <param name="clock"></param>
        /// <returns></returns>
        public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue,
            AnimationClock clock)

        {
            var fromValue = From.Value;

            var toValue = To.Value;


            if (fromValue > toValue)
                return new GridLength((1 - clock.CurrentProgress.Value) *
                                      (fromValue - toValue) + toValue, GridUnitType.Star);

            return new GridLength(clock.CurrentProgress.Value *
                                  (toValue - fromValue) + fromValue, GridUnitType.Star);
        }
    }
}