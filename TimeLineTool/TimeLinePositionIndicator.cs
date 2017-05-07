using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace TimeLineTool
{
   public class TimeLinePositionIndicator : Control
   {
      public Stopwatch CurrentPosition;

      #region Unit Size Property
      const Double UnitSizeDefault = 1.0;

      public Double UnitSize
      {
         get { return (Double)GetValue(UnitSizeProperty); }
         set { SetValue(UnitSizeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for UnitSize.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty UnitSizeProperty =
         DependencyProperty.Register("UnitSize", typeof(Double), typeof(TimeLinePositionIndicator), new UIPropertyMetadata(UnitSizeDefault, new PropertyChangedCallback(OnUnitSizeChanged)));
      private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLinePositionIndicator tc = d as TimeLinePositionIndicator;
         if (tc != null)
         {
            tc.UnitSize = (Double)e.NewValue;
         }
      }
      #endregion

      public TimeLinePositionIndicator()
      {
         DataContext = this;

         CurrentPosition = new Stopwatch();
      }

      public override void OnApplyTemplate()
      {
         base.OnApplyTemplate();
      }
   }
}
