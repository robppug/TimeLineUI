using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Input;

namespace TimeLineTool
{
	//public class TimeLineFunctionControl:ContentPresenter
	public class TimeLineFunctionControl : Button
	{
      public FunctionTriggerEdge CurrentEdge = FunctionTriggerEdge.Waiting;

      private const string BackGroundColour = "BgBrush";
      private const string BackGroundTriggeredColour = "BgBrushTriggered";


      private Boolean _ready = true;
		internal Boolean ReadyToDraw
		{
			get { return _ready; }
			set
			{
				_ready = value;
			}
		}

        public Boolean IsExpanded
        {
            get { return (Boolean)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsExpanded.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register("IsExpanded", typeof(Boolean), typeof(TimeLineFunctionControl), new UIPropertyMetadata(false));



      #region Unit Size Property Handling
      const Double DefaultUnitSize = 5.0;
		public Double UnitSize
		{
			get { return (Double)GetValue(UnitSizeProperty); }
			set { SetValue(UnitSizeProperty, value); }
		}

		// Using a DependencyProperty as the backing store for UnitSize.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty UnitSizeProperty =
			DependencyProperty.Register("UnitSize", typeof(Double), typeof(TimeLineFunctionControl), new UIPropertyMetadata(DefaultUnitSize, new PropertyChangedCallback(OnUnitSizeChanged)));

		private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			TimeLineFunctionControl ctrl = d as TimeLineFunctionControl;
			if (ctrl != null)
			{
				ctrl.PlaceOnCanvas();
			}
		}
      #endregion

      #region ViewLevel
      const TimeLineViewLevel ViewLevelDefault = TimeLineViewLevel.Seconds;

      /// <summary>
      /// Time Line Item View Scale
      /// </summary>
      public TimeLineViewLevel ViewLevel
		{
			get { return (TimeLineViewLevel)GetValue(ViewLevelProperty); }
			set { SetValue(ViewLevelProperty, value); }
		}

		// Using a DependencyProperty as the backing store for ViewLevel.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty ViewLevelProperty =
			DependencyProperty.Register("ViewLevel", typeof(TimeLineViewLevel), typeof(TimeLineFunctionControl), new UIPropertyMetadata(ViewLevelDefault, new PropertyChangedCallback(OnViewLevelChanged)));
		private static void OnViewLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			TimeLineFunctionControl ctrl = d as TimeLineFunctionControl;
			if (ctrl != null)
			{
				ctrl.PlaceOnCanvas();
			}
		}
      #endregion

      #region TimeLineItemControl Begin/Start/End Time Properties
         #region TimeLine Start Time
         public TimeSpan TimeLineStartTime
		   {
			   get { return (TimeSpan)GetValue(TimeLineStartTimeProperty); }
			   set { SetValue(TimeLineStartTimeProperty, value); }
		   }

		   // Using a DependencyProperty as the backing store for TimeLineStartTime.  This enables animation, styling, binding, etc...
		   public static readonly DependencyProperty TimeLineStartTimeProperty =
			   DependencyProperty.Register("TimeLineStartTime", typeof(TimeSpan), typeof(TimeLineFunctionControl), new UIPropertyMetadata(TimeSpan.Zero, new PropertyChangedCallback(OnTimeValueChanged)));
		   #endregion

		   #region Actual Start Time
		   public TimeSpan StartTime
		   {
			   get { return (TimeSpan)GetValue(StartTimeProperty); }
			   set { SetValue(StartTimeProperty, value); }
		   }

		   // Using a DependencyProperty as the backing store for StartTime.  This enables animation, styling, binding, etc...
		   public static readonly DependencyProperty StartTimeProperty =
			   DependencyProperty.Register("StartTime", typeof(TimeSpan), typeof(TimeLineFunctionControl), new UIPropertyMetadata(TimeSpan.Zero, new PropertyChangedCallback(OnTimeValueChanged)));
		   #endregion

		   #region Actual End Time
		   public TimeSpan EndTime
		   {
			   get { return (TimeSpan)GetValue(EndTimeProperty); }
			   set { SetValue(EndTimeProperty, value); }
		   }

		   // Using a DependencyProperty as the backing store for EndTime.  This enables animation, styling, binding, etc...
		   public static readonly DependencyProperty EndTimeProperty =
			   DependencyProperty.Register("EndTime", typeof(TimeSpan), typeof(TimeLineFunctionControl), new UIPropertyMetadata(TimeSpan.MinValue.Add(TimeSpan.FromMinutes(5)), new PropertyChangedCallback(OnTimeValueChanged)));
         #endregion
      #endregion

      /// <summary>
      /// This is the EDIT mouse hover threshold (in PIXELS) used when the user attempts to modify the START/END times of the Function Control.
      /// </summary>
      public Double EditBorderThreshold
      {
         get { return (Double)GetValue(EditBorderThresholdProperty); }
         set { SetValue(EditBorderThresholdProperty, value); }
      }

      // Using a DependencyProperty as the backing store for EditBorderThreshold.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty EditBorderThresholdProperty =
         DependencyProperty.Register("EditBorderThreshold", typeof(Double), typeof(TimeLineFunctionControl), new UIPropertyMetadata(4.0, new PropertyChangedCallback(OnEditThresholdChanged)));

      private static void OnEditThresholdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineFunctionControl ctrl = d as TimeLineFunctionControl;
      }

		private static void OnTimeValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
            TimeLineFunctionControl ctrl = d as TimeLineFunctionControl;
            if (ctrl != null)
                ctrl.PlaceOnCanvas();
		}

		internal void PlaceOnCanvas()
		{
			var w = CalculateWidth();

			if (w > 0)
				Width = w;

			var p = CalculateLeftPosition();

			if (p >= 0)
			{
				Canvas.SetLeft(this, p);
			}
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
		}

      private ContentPresenter _LeftIndicator;
      private ContentPresenter _RightIndicator;

      public override void OnApplyTemplate()
      {
         (DataContext as FunctionDataType).BgBrush = (Brush)Application.Current.MainWindow.FindResource(BackGroundColour);

         _LeftIndicator = Template.FindName("EditBar_LeftIndicator", this) as ContentPresenter;
         _RightIndicator = Template.FindName("EditBar_RightIndicator", this) as ContentPresenter;

         if (_LeftIndicator != null)
               _LeftIndicator.Visibility = Visibility.Collapsed;

         if (_RightIndicator != null)
               _RightIndicator.Visibility = Visibility.Collapsed;

         base.OnApplyTemplate();
      }

      public void OnTrigger(object sender, FunctionTriggerEventArgs e)
      {
         switch (CurrentEdge)
         {
            case FunctionTriggerEdge.Start:
               (DataContext as FunctionDataType).BgBrush = (Brush)Application.Current.MainWindow.FindResource(BackGroundTriggeredColour);
               break;

            case FunctionTriggerEdge.Waiting:
            case FunctionTriggerEdge.End:
            default:
               (DataContext as FunctionDataType).BgBrush = (Brush)Application.Current.MainWindow.FindResource(BackGroundColour);
               break;
         }
      }

      internal Double CalculateWidth()
		{	
			try
			{
            TimeSpan start = (TimeSpan)GetValue(StartTimeProperty);
            TimeSpan end = (TimeSpan)GetValue(EndTimeProperty);
				TimeSpan duration = end.Subtract(start);

				return ConvertTimeToDistance(duration);
			}
			catch (Exception)
			{
				return 0;
			}
		}

		internal Double CalculateLeftPosition()
		{
         TimeSpan start = (TimeSpan)GetValue(StartTimeProperty);
         TimeSpan timelinestart = (TimeSpan)GetValue(TimeLineStartTimeProperty);

			TimeSpan Duration = start.Subtract(timelinestart);
			return ConvertTimeToDistance(Duration);
		}

      /// <summary>
      /// Show/Hide handling of Edit bars.
      /// </summary>
      /// <param name="left"></param>
      /// <param name="right"></param>
      private void SetEditIndicators(Visibility left, Visibility right)
      {
         if (_LeftIndicator != null)
         {
            _LeftIndicator.Visibility = left;
         }
         if (_RightIndicator != null)
         {
            _RightIndicator.Visibility = right;
         }
      }
		#region Conversion Utilities
		private Double ConvertTimeToDistance(TimeSpan span)
		{
			TimeLineViewLevel lvl = (TimeLineViewLevel)GetValue(ViewLevelProperty);
			Double unitSize = (Double)GetValue(UnitSizeProperty);
			Double value = unitSize;
			switch (lvl)
			{
            case TimeLineViewLevel.MilliSeconds:
               value = span.TotalMilliseconds * unitSize;
               break;
            case TimeLineViewLevel.Seconds:
               value = span.TotalSeconds * unitSize;
               break;
            case TimeLineViewLevel.Minutes:
               value = span.TotalMinutes * unitSize;
               break;
            case TimeLineViewLevel.Hours:
					value = span.TotalHours * unitSize;
					break;
				case TimeLineViewLevel.Days:
					value = span.TotalDays * unitSize;
					break;
				case TimeLineViewLevel.Weeks:
					value = (span.TotalDays / 7.0) * unitSize;
					break;
				case TimeLineViewLevel.Months:
					value = (span.TotalDays / 30.0) * unitSize;
					break;
				case TimeLineViewLevel.Years:
					value = (span.TotalDays / 365.0) * unitSize;
					break;
				default:
					break;
			}
			return value;
		}

		private TimeSpan ConvertDistanceToTime(Double distance)
		{
			TimeLineViewLevel lvl = (TimeLineViewLevel)GetValue(ViewLevelProperty);
			Double unitSize = (Double)GetValue(UnitSizeProperty);
			double seconds, minutes, hours, days, weeks, months, years, milliseconds = 0;

			switch (lvl)
			{
            case TimeLineViewLevel.MilliSeconds:
               //value = span.TotalMinutes * unitSize;
               milliseconds = (distance / unitSize);
               //convert to milliseconds
               milliseconds = milliseconds * 1;
               break;
            case TimeLineViewLevel.Seconds:
               //value = span.TotalMinutes * unitSize;
               seconds = (distance / unitSize);
               //convert to milliseconds
               milliseconds = seconds * 1000;
               break;
            case TimeLineViewLevel.Minutes:
               //value = span.TotalMinutes * unitSize;
               minutes = (distance / unitSize);
               //convert to milliseconds
               milliseconds = minutes * 60000;
               break;
            case TimeLineViewLevel.Hours:
					hours = (distance / unitSize);
					//convert to milliseconds
					milliseconds = hours * 60 * 60000;
					break;
				case TimeLineViewLevel.Days:
					days = (distance / unitSize);
					//convert to milliseconds
					milliseconds = days * 24 * 60 * 60000;
					break;
				case TimeLineViewLevel.Weeks:
					//value = (span.TotalDays / 7.0) * unitSize;
					weeks = (7 * distance / unitSize);
					//convert to milliseconds
					milliseconds = weeks * 7 * 24 * 60 * 60000;
					break;
				case TimeLineViewLevel.Months:
					months = (30 * distance / unitSize);
					//convert to milliseconds
					milliseconds = months * 30 * 24 * 60 * 60000;
					break;
				case TimeLineViewLevel.Years:
					years = (365 * distance / unitSize);
					//convert to milliseconds
					milliseconds = years * 365 * 24 * 60 * 60000;
					break;
				default:
					break;
			}
			long ticks = (long)milliseconds * 10000;
			TimeSpan returner = new TimeSpan(ticks);
			return returner;

			//return new TimeSpan(0, 0, 0, 0, (int)milliseconds);
		}

		#endregion

        
      /// <summary>
      /// Handling of MOUSE hover over a TimeLineFunctionControl (Left/Right Click, Edit etc...)
      /// </summary>
      /// <param name="e"></param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            switch (GetClickAction())
            {
                case TimeLineAction.StretchStart:
                    SetEditIndicators(Visibility.Visible, Visibility.Collapsed);
                    break;
                case TimeLineAction.StretchEnd:
                    SetEditIndicators(Visibility.Collapsed, Visibility.Visible);
                    break;
                default:
                    SetEditIndicators(Visibility.Collapsed, Visibility.Collapsed);
                    break;
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            SetEditIndicators(Visibility.Collapsed, Visibility.Collapsed);
			
            if (IsExpanded && (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed))
            {
                return;
            }
			
			IsExpanded = false;
            
			base.OnMouseLeave(e);
        }

		#region Manipulation Tools
		internal TimeLineAction GetClickAction()
		{	
			var X = Mouse.GetPosition(this).X;
         Double borderThreshold = (Double)GetValue(EditBorderThresholdProperty);
			Double unitsize = (Double)GetValue(UnitSizeProperty);

         /* Are they trying to move the START (left) time */
         if (X < borderThreshold)
         {
            return TimeLineAction.StretchStart;
         }
         /* Are they trying to move the END (right) time */
         if (X > Width - borderThreshold)
         {
            return TimeLineAction.StretchEnd;
         }

         /* They are moving the entire FunctionControl */
         return TimeLineAction.Move;
		}
		
		internal bool CanDelta(int StartOrEnd, Double deltaX)
		{
			Double unitS = (Double)GetValue(UnitSizeProperty);
         Double threshold = unitS / 100.0;
			Double newW = unitS;
			if (StartOrEnd == 0)//we are moving the start
			{
				if (deltaX < 0)
					return true;
				//otherwises get what our new width would be
				newW = Width - deltaX;//delta is + but we are actually going to shrink our width by moving start +
				return newW > threshold;
			}
			else
			{
				if (deltaX > 0)
					return true;
				newW = Width + deltaX;
				return newW > threshold;
			}
		}
		
        internal TimeSpan GetDeltaTime(Double deltaX)
		{
			return ConvertDistanceToTime(deltaX);
		}
		
		internal void GetPlacementInfo(ref Double left, ref Double width, ref Double end)
		{
			left = Canvas.GetLeft(this);
			width = Width;
			end = left + Width;
			//Somewhere on the process of removing a timeline control from the visual tree
			//it resets our start time to min value.  In that case it then results in ridiculous placement numbers
			//that this feeds to the control and crashes the whole app in a strange way.
			if(TimeLineStartTime == TimeSpan.MinValue)
			{
				left = 0;
				width = 1;
				end = 1;
			}
		}

      /// <summary>
      /// Moves the entire FunctionControl (Start and End times)
      /// </summary>
      /// <param name="deltaX"></param>
		internal void MoveMe(Double deltaX)
		{
			var left = Canvas.GetLeft(this);
			left += deltaX;
			if (left < 0)
				left = 0;
			Canvas.SetLeft(this, left);
			
			TimeSpan startTs = ConvertDistanceToTime(left);
         TimeSpan tlStart = TimeLineStartTime;
         TimeSpan s = StartTime;
         TimeSpan e = EndTime;
			TimeSpan duration = e.Subtract(s);

			StartTime = tlStart.Add(startTs);
			EndTime = StartTime.Add(duration);
		 
		}
		#endregion

      /// <summary>
      /// Moves the FunctionControl End time.
      /// </summary>
      /// <param name="delta">Amount to move in PIXELS.</param>
		internal void MoveEndTime(double delta)
		{
			Width += delta;
         //calculate our new end time
         TimeSpan s = (TimeSpan)GetValue(StartTimeProperty);
			TimeSpan ts = ConvertDistanceToTime(Width);
			EndTime = s.Add(ts);
		}

      /// <summary>
      /// Moves the FunctionControl Start time.
      /// </summary>
      /// <param name="delta">Amount to move in PIXELS.</param>
		internal void MoveStartTime(double delta)
		{
			Double curLeft = Canvas.GetLeft(this);
			if (curLeft == 0 && delta < 0)
				return;
			curLeft += delta;
			Width = Width - delta;
			if (curLeft < 0)
			{
				//we need to 
				Width -= curLeft;//We are moving back to 0 and have to fix our width to not bump a bit.
				curLeft = 0;
			}
			Canvas.SetLeft(this, curLeft);
			//recalculate start time;
			TimeSpan ts = ConvertDistanceToTime(curLeft);
			StartTime = TimeLineStartTime.Add(ts);
			
		}

		internal void MoveToNewStartTime(TimeSpan start)
		{
         TimeSpan s = (TimeSpan)GetValue(StartTimeProperty);
         TimeSpan e = (TimeSpan)GetValue(EndTimeProperty);
			TimeSpan duration = e.Subtract(s);
			StartTime = start;
			EndTime = start.Add(duration);
			PlaceOnCanvas();
			 
		}
		/// <summary>
		/// Sets up with a default of 55 of our current units in size.
		/// </summary>
		internal void InitializeDefaultLength()
		{
			TimeSpan duration = ConvertDistanceToTime(10 * (Double)GetValue(UnitSizeProperty));
			EndTime = StartTime.Add(duration);
			Width = CalculateWidth();
		}
	}
}