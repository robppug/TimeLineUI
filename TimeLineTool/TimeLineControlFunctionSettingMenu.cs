using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace TimeLineTool
{
   public class TimeLineControlFunctionSettingMenu : Control
   {
      #region SettingMenu Properties
      #region Actual Start Time
      public String StartTime
      {
         get { return (String)GetValue(StartTimeProperty); }
         set { SetValue(StartTimeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for StartTime.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty StartTimeProperty =
         DependencyProperty.Register("StartTime", typeof(String), typeof(TimeLineControlFunctionSettingMenu), new UIPropertyMetadata("0", new PropertyChangedCallback(OnTimeValueChanged)));
      #endregion

      #region Actual End Time
      public String EndTime
      {
         get { return (String)GetValue(EndTimeProperty); }
         set { SetValue(EndTimeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for EndTime.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty EndTimeProperty =
         DependencyProperty.Register("EndTime", typeof(String), typeof(TimeLineControlFunctionSettingMenu), new UIPropertyMetadata("100", new PropertyChangedCallback(OnTimeValueChanged)));
      #endregion
      
      private static void OnTimeValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         //throw new NotImplementedException();
      }

      #region Function
      public String Function
      {
         get { return (String)GetValue(FunctionProperty); }
         set { SetValue(FunctionProperty, value); }
      }

      // Using a DependencyProperty as the backing store for EndTime.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty FunctionProperty =
         DependencyProperty.Register("Function", typeof(String), typeof(TimeLineControlFunctionSettingMenu), new UIPropertyMetadata("None", new PropertyChangedCallback(OnFunctionValueChanged)));

      private static void OnFunctionValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         //throw new NotImplementedException();
      }
      #endregion

      #endregion

      public event EventHandler Closed;

      public TimeLineControlFunctionSettingMenu()
      {
         DataContext = this;

         KeyUp += TimeLineControlFunctionSettingMenu_KeyUp;
         //Binding startBinding = new Binding("Function");
         //startBinding.Mode = BindingMode.TwoWay;
         //startBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;

         //SetBinding(FunctionProperty, startBinding);
      }

      public override void OnApplyTemplate()
      {
         base.OnApplyTemplate();

         /* Handle the CLOSE button click defined in the XAML STYLE */
         ((Button)GetTemplateChild("settingMenuCloseButton")).Click += buttonCloseSettingMenu;
      }

      private void TimeLineControlFunctionSettingMenu_KeyUp(object sender, KeyEventArgs e)
      {
         if(e.Key == Key.Escape)
         {
            Closed?.Invoke(this, EventArgs.Empty);
         }
      }

      public void buttonCloseSettingMenu(object sender, RoutedEventArgs e)
      {
         Closed?.Invoke(this, EventArgs.Empty);
      }
   }
}
