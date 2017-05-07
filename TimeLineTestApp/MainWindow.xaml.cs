using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TimeLineTool;
using System.Collections.ObjectModel;

namespace TimeLineTestApp
{
    /*Notes:
     * This simple little demo app doesn't leverage data binding, and doesn't demonstrate some of the things that are available.
     * It does give you a feel for how you can do everything, and should give someone a start so they knw what they can do via more poweful data binding practices.
     */ 
	
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		ObservableCollection<ITimeLineDataItem> data = new ObservableCollection<ITimeLineDataItem>();
        public ObservableCollection<ITimeLineDataItem> t2Data = new ObservableCollection<ITimeLineDataItem>();
        public ObservableCollection<ITimeLineDataItem> t3Data = new ObservableCollection<ITimeLineDataItem>();
		ObservableCollection<ITimeLineDataItem> listboxData = new ObservableCollection<ITimeLineDataItem>();

		public MainWindow()
		{
			InitializeComponent();

			var tmp1 = new FunctionDataType()
			{
				StartTime = TimeSpan.FromMilliseconds(0),
				EndTime = TimeSpan.FromMilliseconds(200),
				Name = "Temp 1",
            BgBrush = new SolidColorBrush(Colors.AntiqueWhite)
			};
			var tmp2 = new FunctionDataType()
			{
            StartTime = TimeSpan.FromMilliseconds(10),
            EndTime = TimeSpan.FromMilliseconds(20),
            Name = "Temp 2",
            BgBrush = new SolidColorBrush(Colors.AntiqueWhite)
         };
			var temp3 = new FunctionDataType()
			{
            StartTime = TimeSpan.FromMilliseconds(30),
            EndTime = TimeSpan.FromMilliseconds(60),
            Name = "Temp 3",
            BgBrush = new SolidColorBrush(Colors.AntiqueWhite)
         };
			var temp4 = new FunctionDataType()
			{
            StartTime = TimeSpan.FromMilliseconds(60),
            EndTime = TimeSpan.FromMilliseconds(70),
            Name = "Temp 4",
            BgBrush = new SolidColorBrush(Colors.AntiqueWhite)
         };

			data.Add(tmp1);
			data.Add(tmp2);
			data.Add(temp3);
			data.Add(temp4);

         t2Data.Add(tmp1);
         t3Data.Add(temp3);

			//TimeLine2.StartTime = TimeSpan.Zero;
            
         //TimeLine3.StartTime = TimeSpan.Zero;
         //TimeLine2.Functions = t2Data;
         //TimeLine3.Functions = t3Data;

			var lb1 = new FunctionDataType()
			{
				Name = "ListBox 1"
			};
			var lb2 = new FunctionDataType()
			{
				Name = "ListBox 2"
			};
			var lb3 = new FunctionDataType()
			{
				Name = "ListBox 3"
			};
			var lb4 = new FunctionDataType()
			{
				Name = "ListBox 4"
			};
			listboxData.Add(lb1);
			listboxData.Add(lb2);
			listboxData.Add(lb3);
			listboxData.Add(lb4);
			ListSrc.ItemsSource = listboxData;

         listBoxViewLevel.Items.Add(TimeLineViewLevel.MilliSeconds);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Seconds);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Minutes);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Hours);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Days);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Weeks);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Months);
         listBoxViewLevel.Items.Add(TimeLineViewLevel.Years);
         //listBoxViewLevel.SelectedIndex = (int)TimeLine2.ViewLevel;
      }

      private void Slider_Scale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
         foreach(TimeLineControl tlc in TimeLineControls.Items)
         {
            tlc.UnitSize = Slider_Scale.Value;
         }
		}

      private void listBoxViewLevel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         //TimeLine2.ViewLevel = (TimeLineViewLevel)(sender as ListBox).SelectedIndex;
         //TimeLine3.ViewLevel = (TimeLineViewLevel)(sender as ListBox).SelectedIndex;
      }

      private void buttonTrigger_Click(object sender, RoutedEventArgs e)
      {
         foreach (TimeLineControl tlc in TimeLineControls.Items)
         {
            tlc.StartPositionLineIndication();
         }
      }

      private void buttonAddNewTimeLine_Click(object sender, RoutedEventArgs e)
      {
         TimeLineControl tlc = new TimeLineControl()
         {
            StartTime = TimeSpan.Zero,
            Height = 80,
            FunctionTemplate = (DataTemplate)FindResource("TimeLineFunctionDataTemplate"),
            PositionLineTemplate = (ControlTemplate)FindResource("TimeLinePositionIndicator"),
            HorizontalAlignment = HorizontalAlignment.Left,
            MinimumUnitWidth = 20,
            Background = new SolidColorBrush(Colors.AntiqueWhite),
            DrawTimeGrid = true,
            SnapToGrid = true,
            SnapToUnitSize = 0.001,
            Width = 1000,
            MinWidth = 1000,
            SynchedWithSiblings = true,
            //Functions = new ObservableCollection<ITimeLineDataItem>(),
         };

         tlc.OnTimerTick += TimeLineControlTick;
         TimeLineControls.Items.Add(tlc);
      }

      private void TimeLineControlTick(object sender, FunctionTriggerEventArgs e)
      {
         if(e.Edge == FunctionTriggerEdge.End)
         {
            TimeLineScrollView.ScrollToHome();
         }
         else if(e.TimeStamp.Value.TotalMilliseconds > (sender as TimeLineControl).ConvertDistanceToTime(TimeLineScrollView.ActualWidth + TimeLineScrollView.HorizontalOffset).TotalMilliseconds)
         {
            TimeLineScrollView.ScrollToHorizontalOffset(TimeLineScrollView.HorizontalOffset + TimeLineScrollView.ActualWidth);
         }
      }
   }
}
