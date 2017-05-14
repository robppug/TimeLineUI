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
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Windows.Controls.Primitives;
using System.Collections.Specialized;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows.Media.Animation;

namespace TimeLineTool
{

   public enum TimeLineManipulationMode { Linked, Free }
   public enum FunctionTriggerEdge { Waiting, Start, End }
   internal enum TimeLineAction { Move, StretchStart, StretchEnd }

   internal class TimeLineItemChangedEventArgs : EventArgs
   {
      public TimeLineManipulationMode Mode { get; set; }
      public TimeLineAction Action { get; set; }
      public TimeSpan DeltaTime { get; set; }
      public Double DeltaX { get; set; }
   }

   internal class TimeLineDragAdorner : Adorner
   {
      private ContentPresenter _adorningContentPresenter;
      internal ITimeLineDataItem Data { get; set; }
      internal DataTemplate Template { get; set; }
      Point _mousePosition;
      public Point MousePosition
      {
         get
         {
               return _mousePosition;
         }
         set
         {
               if (_mousePosition != value)
               {
                  _mousePosition = value;
                  _layer.Update(AdornedElement);
               }

         }
      }

      AdornerLayer _layer;
      public TimeLineDragAdorner(TimeLineFunctionControl uiElement, DataTemplate template)
         : base(uiElement)
      {
         _adorningContentPresenter = new ContentPresenter();
         _adorningContentPresenter.Content = uiElement.DataContext;
         _adorningContentPresenter.ContentTemplate = template;
         _adorningContentPresenter.Opacity = 0.5;
         _layer = AdornerLayer.GetAdornerLayer(uiElement);

         _layer.Add(this);
         IsHitTestVisible = false;

      }
      public void Detach()
      {
         _layer.Remove(this);
      }
      protected override Visual GetVisualChild(int index)
      {
         return _adorningContentPresenter;
      }

      protected override Size MeasureOverride(Size constraint)
      {
         //_adorningContentPresenter.Measure(constraint);
         return new Size((AdornedElement as TimeLineFunctionControl).Width, (AdornedElement as TimeLineFunctionControl).DesiredSize.Height);//(_adorningContentPresenter.Width,_adorningContentPresenter.Height);
      }

      protected override int VisualChildrenCount
      {
         get
         {
               return 1;
         }
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         _adorningContentPresenter.Arrange(new Rect(finalSize));
         return finalSize;
      }

      public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
      {
         GeneralTransformGroup result = new GeneralTransformGroup();
         result.Children.Add(base.GetDesiredTransform(transform));
         result.Children.Add(new TranslateTransform(MousePosition.X - 4, MousePosition.Y - 4));
         return result;
      }

   }

   public enum TimeLineViewLevel { MilliSeconds, Seconds, Minutes, Hours, Days, Weeks, Months, Years };

   public class FunctionTriggerEventArgs : EventArgs
   {
      public TimeSpan? TimeStamp;
      public FunctionTriggerEdge? Edge;

      public FunctionTriggerEventArgs(TimeSpan? ts, FunctionTriggerEdge? edge)
      {
         TimeStamp = ts;
         Edge = edge;
      }
   }

   public class TimeLineControl : Canvas
   {
      public event EventHandler<FunctionTriggerEventArgs> OnTimerTick;
      public Panel HostCanvas;

      public static TimeSpan CalculateMinimumAllowedTimeSpan(double unitSize)
      {
         //desired minimum widh for these manipulations = 10 pixels
         int minPixels = 10;
         double hours = minPixels / unitSize;
            
         //convert to milliseconds
         long ticks = (long)(hours * 60 * 60000 * 10000);

         return new TimeSpan(ticks);
      }

        private Double _bumpThreshold = 1.5;
        private ScrollViewer _scrollViewer;
        private Canvas _gridCanvas;
        static TimeLineDragAdorner _dragAdorner;
        static TimeLineDragAdorner DragAdorner
        {
            get
            {
                return _dragAdorner;
            }
            set
            {
                if (_dragAdorner != null)
                    _dragAdorner.Detach();
                _dragAdorner = value;
            }
        }
        private Boolean _synchedWithSiblings = true;
        public Boolean SynchedWithSiblings
        {
            get
            {
                return _synchedWithSiblings;
            }
            set
            {
                _synchedWithSiblings = value;
            }
        }
        internal Boolean _isSynchInstigator = false;
        internal Double SynchWidth = 0;

        Boolean _functionsInitialized = false;

        Boolean _unitSizeInitialized = false;
        Boolean _startTimeInitialized = false;


      #region Dependancy Properties
      public ITimeLineDataItem FocusOnItem
      {
         get { return (ITimeLineDataItem)GetValue(FocusOnItemProperty); }
         set { SetValue(FocusOnItemProperty, value); }
      }

      // Using a DependencyProperty as the backing store for FocusOnItem.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty FocusOnItemProperty =
         DependencyProperty.Register("FocusOnItem", typeof(ITimeLineDataItem), typeof(TimeLineControl), new UIPropertyMetadata(null, new PropertyChangedCallback(FocusItemChanged)));
      public static void FocusItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if ((e.NewValue != null) && (tc != null))
         {
               tc.ScrollToItem(e.NewValue as ITimeLineDataItem);
         }
      }

      private void ScrollToItem(ITimeLineDataItem target)
      {
         Double tgtNewWidth = 0;
         Double maxUnitSize = 450;//28000;
         Double minUnitSize = 1;
         if (_scrollViewer != null)
         {
               for (int i = 1; i < Children.Count; i++)
               {
                  var ctrl = Children[i] as TimeLineFunctionControl;
                  if (ctrl != null && ctrl.DataContext == target)
                  {
                     Double curW = ctrl.Width;
                     if (curW < 5)
                     {
                           tgtNewWidth = 50;
                     }
                     else if (curW > _scrollViewer.ViewportWidth)
                     {
                           tgtNewWidth = _scrollViewer.ViewportWidth / 3;
                     }

                     if (tgtNewWidth != 0)
                     {
                           Double newUnitSize = (UnitSize * tgtNewWidth) / curW;
                           if (newUnitSize > maxUnitSize)
                              newUnitSize = maxUnitSize;
                           else if (newUnitSize < minUnitSize)
                              newUnitSize = minUnitSize;
                           UnitSize = newUnitSize;
                           SynchronizeSiblings();
                     }
                     ctrl.BringIntoView();
                     return;
                  }
               }
         }
      }


        #region manager
        public ITimeLineManager Manager
        {
            get { return (ITimeLineManager)GetValue(ManagerProperty); }
            set { SetValue(ManagerProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Manager.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ManagerProperty =
            DependencyProperty.Register("Manager", typeof(ITimeLineManager), typeof(TimeLineControl),
            new UIPropertyMetadata(null));

      #endregion

      #region Min Width Property
      const Double MinWidthDefault = 0.0;
      public Double MinWidth
      {
         get { return (Double)GetValue(MinWidthProperty); }
         set { SetValue(MinWidthProperty, value); }
      }

      // Using a DependencyProperty as the backing store for MinWidth.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty MinWidthProperty =
         DependencyProperty.Register("MinWidth", typeof(Double), typeof(TimeLineControl), new UIPropertyMetadata(MinWidthDefault));
      #endregion

      #region Min Height Property
      const Double MinHeightDefault = 0.0;
      public Double MinHeight
      {
         get { return (Double)GetValue(MinHeightProperty); }
         set { SetValue(MinHeightProperty, value); }
      }

      // Using a DependencyProperty as the backing store for MinHeight.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty MinHeightProperty =
         DependencyProperty.Register("MinHeight", typeof(Double), typeof(TimeLineControl), new UIPropertyMetadata(MinHeightDefault));
      #endregion

      #region background and grid dependency properties
      #region Minimum Unit Width Property
      const Double MinimumUnitWidthDefault = 10.0;

      /// <summary>
      /// Gets or sets the GRIDs minimum width.
      /// </summary>
      public Double MinimumUnitWidth
      {
         get { return (Double)GetValue(MinimumUnitWidthProperty); }
         set { SetValue(MinimumUnitWidthProperty, value); }
      }

      // Using a DependencyProperty as the backing store for MinimumUnitWidth.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty MinimumUnitWidthProperty =
         DependencyProperty.Register("MinimumUnitWidth", typeof(Double), typeof(TimeLineControl), new UIPropertyMetadata(MinimumUnitWidthDefault, new PropertyChangedCallback(OnBackgroundValueChanged)));
      #endregion

      #region snap to grid
      public Boolean SnapToGrid
      {
         get { return (Boolean)GetValue(SnapToGridProperty); }
         set { SetValue(SnapToGridProperty, value); }
      }

      // Using a DependencyProperty as the backing store for SnapToGrid.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty SnapToGridProperty =
          DependencyProperty.Register("SnapToGrid", typeof(Boolean), typeof(TimeLineControl),
              new UIPropertyMetadata(null));
      //new UIPropertyMetadata(false,
      //new PropertyChangedCallback(OnBackgroundValueChanged)));
      #endregion

      #region snap to Unit Size
      const double SnapToUnitSizeDefault = 1;
      public double SnapToUnitSize
      {
         get { return (double)GetValue(SnapToUnitSizeProperty); }
         set { SetValue(SnapToUnitSizeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for SnapToUnitSize.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty SnapToUnitSizeProperty =
          DependencyProperty.Register("SnapToUnitSize", typeof(double), typeof(TimeLineControl), new UIPropertyMetadata(SnapToUnitSizeDefault));
      #endregion

      #region Draw Time Grid Property
      public Boolean DrawTimeGrid
      {
         get { return (Boolean)GetValue(DrawTimeGridProperty); }
         set { SetValue(DrawTimeGridProperty, value); }
      }

      // Using a DependencyProperty as the backing store for DrawTimeGrid.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty DrawTimeGridProperty =
         DependencyProperty.Register("DrawTimeGrid", typeof(Boolean), typeof(TimeLineControl), new UIPropertyMetadata(false, new PropertyChangedCallback(OnDrawTimeGridChanged)));
      #endregion

        #region minor unit thickness
        public int MinorUnitThickness
        {
            get { return (int)GetValue(MinorUnitThicknessProperty); }
            set { SetValue(MinorUnitThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinorUnitThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinorUnitThicknessProperty =
            DependencyProperty.Register("MinorUnitThickness", typeof(int), typeof(TimeLineControl),
                        new UIPropertyMetadata(1,
                            new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion

        #region major unit thickness
        public int MajorUnitThickness
        {
            get { return (int)GetValue(MajorUnitThicknessProperty); }
            set { SetValue(MajorUnitThicknessProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MajorUnitThickness.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MajorUnitThicknessProperty =
            DependencyProperty.Register("MajorUnitThickness", typeof(int), typeof(TimeLineControl),
                new UIPropertyMetadata(2, new PropertyChangedCallback(OnBackgroundValueChanged)));
        #endregion
        private static byte _defaultColour = 20;

      #region Day Line Brush Property
      public Brush DayLineBrush
      {
         get { return (Brush)GetValue(DayLineBrushProperty); }
         set { SetValue(DayLineBrushProperty, value); }
      }

      // Using a DependencyProperty as the backing store for DayLineBrush.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty DayLineBrushProperty =
         DependencyProperty.Register("DayLineBrush", typeof(Brush), typeof(TimeLineControl),
               new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _defaultColour, G = _defaultColour, B = _defaultColour, A = 255 }), new PropertyChangedCallback(OnBackgroundValueChanged)));
      #endregion

        #region hour line brush
        public Brush HourLineBrush
        {
            get { return (Brush)GetValue(HourLineBrushProperty); }
            set { SetValue(HourLineBrushProperty, value); }
        }


        // Using a DependencyProperty as the backing store for HourLineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HourLineBrushProperty =
            DependencyProperty.Register("HourLineBrush", typeof(Brush), typeof(TimeLineControl),
            new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _defaultColour, G = _defaultColour, B = _defaultColour, A = 255 / 2 }),
                new PropertyChangedCallback(OnBackgroundValueChanged)));

        #endregion

        #region minute line brush
        public Brush MinuteLineBrush
        {
            get { return (Brush)GetValue(MinuteLineBrushProperty); }
            set { SetValue(MinuteLineBrushProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MinuteLineBrush.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MinuteLineBrushProperty =
            DependencyProperty.Register("MinuteLineBrush", typeof(Brush), typeof(TimeLineControl),
            new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _defaultColour, G = _defaultColour, B = _defaultColour, A = 255 / 3 }),
                new PropertyChangedCallback(OnBackgroundValueChanged)));
      #endregion

      #region Second Line Brush Property
      public Brush SecondLineBrush
      {
         get { return (Brush)GetValue(SecondLineBrushProperty); }
         set { SetValue(SecondLineBrushProperty, value); }
      }

      // Using a DependencyProperty as the backing store for MinuteLineBrush.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty SecondLineBrushProperty =
          DependencyProperty.Register("SecondLineBrush", typeof(Brush), typeof(TimeLineControl),
          new UIPropertyMetadata(new SolidColorBrush(new Color() { R = _defaultColour, G = _defaultColour, B = _defaultColour, A = 255 / 4 }),
              new PropertyChangedCallback(OnBackgroundValueChanged)));
      #endregion
      private static void OnDrawTimeGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.DrawBackGround(true);
            }
        }

        private static void OnBackgroundValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.DrawBackGround();
            }
        }
        #endregion

        #region Function Template Property
        private DataTemplate _template;
        public DataTemplate FunctionTemplate
        {
            get { return (DataTemplate)GetValue(FunctionTemplateProperty); }
            set { SetValue(FunctionTemplateProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FunctionTemplate.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FunctionTemplateProperty =
            DependencyProperty.Register("FunctionTemplate", typeof(DataTemplate), typeof(TimeLineControl), new UIPropertyMetadata(null, new PropertyChangedCallback(OnFunctionTemplateChanged)));
        private static void OnFunctionTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            TimeLineControl tc = d as TimeLineControl;
            if (tc != null)
            {
                tc.SetTemplate(e.NewValue as DataTemplate);
            }
        }
      #endregion

      #region Position Line Template Property
      private ControlTemplate _positionLineTemplate;
      public ControlTemplate PositionLineTemplate
      {
         get { return (ControlTemplate)GetValue(PositionLineTemplateProperty); }
         set { SetValue(PositionLineTemplateProperty, value); }
      }

      // Using a DependencyProperty as the backing store for PositionLineTemplate.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty PositionLineTemplateProperty =
          DependencyProperty.Register("PositionLineTemplate", typeof(ControlTemplate), typeof(TimeLineControl), new UIPropertyMetadata(null, new PropertyChangedCallback(OnPositionLineTemplateChanged)));
      private static void OnPositionLineTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if (tc != null)
         {
            tc.SetPositionLineTemplate(e.NewValue as ControlTemplate);
         }
      }
      #endregion

      #region Funtions Property
      public ObservableCollection<ITimeLineDataItem> Functions
      {
         get { return (ObservableCollection<ITimeLineDataItem>)GetValue(FunctionsProperty); }
         set { SetValue(FunctionsProperty, value); }
      }

      // Using a DependencyProperty as the backing store for Functions.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty FunctionsProperty =
         DependencyProperty.Register("Functions", typeof(ObservableCollection<ITimeLineDataItem>), typeof(TimeLineControl), new UIPropertyMetadata(null, new PropertyChangedCallback(OnFunctionsChanged)));
      private static void OnFunctionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if (tc != null)
         {
            /* Re-initialise all the ITEMS in the TimeLineControl (i.e Grid and all TimeLineDataItems) */
            tc.InitializeFunctions(e.NewValue as ObservableCollection<ITimeLineDataItem>);

            tc.UpdateUnitSize(tc.UnitSize);

            tc._functionsInitialized = true;

            tc.DrawBackGround();
         }
      }
      #endregion

      #region ViewLevel Property
      const TimeLineViewLevel ViewLevelDefault = TimeLineViewLevel.Seconds;

      /// <summary>
      /// TimeLine View Scale
      /// </summary>
      public TimeLineViewLevel ViewLevel
      {
         get { return (TimeLineViewLevel)GetValue(ViewLevelProperty); }
         set { SetValue(ViewLevelProperty, value); }
      }

      // Using a DependencyProperty as the backing store for ViewLevel.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty ViewLevelProperty =
         DependencyProperty.Register("ViewLevel", typeof(TimeLineViewLevel), typeof(TimeLineControl), new UIPropertyMetadata(ViewLevelDefault, new PropertyChangedCallback(OnViewLevelChanged)));
      private static void OnViewLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if (tc != null)
         {
               tc.UpdateViewLevel((TimeLineViewLevel)e.NewValue);
         }
      }

      private void UpdateViewLevel(TimeLineViewLevel lvl)
      {
         if (Functions == null)
            return;

         for (int i = 0; i < Functions.Count; i++)
         {
            var templatedControl = GetTimeLineFunctionControlAt(i);

            if (templatedControl != null)
               templatedControl.ViewLevel = lvl;
         }

         ReDrawChildren();
         //Now we go back and have to detect if things have been collapsed
      }
      #endregion

      #region Unit Size Property
      const Double UnitSizeDefault = 5.0;

      public Double UnitSize
      {
         get { return (Double)GetValue(UnitSizeProperty); }
         set { SetValue(UnitSizeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for UnitSize.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty UnitSizeProperty =
         DependencyProperty.Register("UnitSize", typeof(Double), typeof(TimeLineControl), new UIPropertyMetadata(UnitSizeDefault, new PropertyChangedCallback(OnUnitSizeChanged)));
      private static void OnUnitSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if (tc != null)
         {
            tc._unitSizeInitialized = true;
            tc.UpdateUnitSize((Double)e.NewValue);
         }
      }
      #endregion

      #region Start Time Property
      public TimeSpan StartTime
      {
         get { return (TimeSpan)GetValue(StartTimeProperty); }
         set { SetValue(StartTimeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for StartDate.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty StartTimeProperty =
         DependencyProperty.Register("StartTime", typeof(TimeSpan), typeof(TimeLineControl),
         new UIPropertyMetadata(TimeSpan.MinValue,
               new PropertyChangedCallback(OnStartTimeChanged)));
      private static void OnStartTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
      {
         TimeLineControl tc = d as TimeLineControl;
         if (tc != null)
         {
               tc._startTimeInitialized = true;
               tc.ReDrawChildren();
         }
      }
      #endregion

      #region Manipulation Mode Property
      public TimeLineManipulationMode ManipulationMode
      {
         get { return (TimeLineManipulationMode)GetValue(ManipulationModeProperty); }
         set { SetValue(ManipulationModeProperty, value); }
      }

      // Using a DependencyProperty as the backing store for ManipulationMode.  This enables animation, styling, binding, etc...
      public static readonly DependencyProperty ManipulationModeProperty =
         DependencyProperty.Register("ManipulationMode", typeof(TimeLineManipulationMode), typeof(TimeLineControl), new UIPropertyMetadata(TimeLineManipulationMode.Free));
      #endregion

      #endregion

      /// <summary>
      /// TimeLineControl Constructor
      /// </summary>
      public TimeLineControl()
      {
         /* Create the CANVAS for the GRID */
         _gridCanvas = new Canvas();

         /* Add it to the Parent CANVAS */
         Children.Add(_gridCanvas);
         Focusable = true;
         KeyDown += OnKeyDown;
         KeyUp += OnKeyUp;
         MouseEnter += TimeLineControl_MouseEnter;
         MouseLeave += TimeLineControl_MouseLeave;
         //Items = new ObservableCollection<ITimeLineDataItem>();

         DragDrop.AddDragOverHandler(this, TimeLineControl_DragOver);
         DragDrop.AddDropHandler(this, TimeLineControl_Drop);
         DragDrop.AddDragEnterHandler(this, TimeLineControl_DragOver);
         DragDrop.AddDragLeaveHandler(this, TimeLineControL_DragLeave);

         AllowDrop = true;

         _scrollViewer = GetParentScrollViewer();

         InitializePositionLineIndicator();
      } 

        #region control life cycle events
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            _scrollViewer = GetParentScrollViewer();
        }


        /*
        /// <summary>
        /// I was unable to track down why this control was locking up when
        /// synchronise with siblings is checked and the parent element is closed etc.
        /// I was getting something with a contextswitchdeadblock that I was wracking my
        /// brain trying to figure out.  The problem only happened when a timeline control
        /// with a child timeline item was present.  I could have n empty timeline controls
        /// with no problem.  Adding one timeline item however caused that error when the parent element
        /// is closed etc.
        /// </summary>
        /// <param name="child"></param>
        protected override void ParentLayoutInvalidated(UIElement child)
        {
            //this event fires when something drags over this or when the control is trying to close
            if (child == _tmpDraggAdornerControl)
                return;
            if (!Children.Contains(child))
                return;
            base.ParentLayoutInvalidated(child);
            SynchedWithSiblings = false;
            //Because this layout invalidated became neccessary, I had to then put null checks on all attempts
            //to get a timeline item control.  There appears to be some UI threading going on so that just checking the children count
            //at the begining of the offending methods was not preventing me from crashing.  
            Children.Clear();
        }*/
        #endregion
        #region miscellaneous helpers
        private ScrollViewer GetParentScrollViewer()
        {
            DependencyObject item = VisualTreeHelper.GetParent(this);
            while (item != null)
            {
                String name = "";
                var ctrl = item as Control;
                if (ctrl != null)
                    name = ctrl.Name;
                if (item is ScrollViewer)
                {
                    return item as ScrollViewer;
                }
                item = VisualTreeHelper.GetParent(item);
            }
            return null;
        }

        private void SetTemplate(DataTemplate dataTemplate)
        {
            _template = dataTemplate;
            for (int i = 0; i < Children.Count; i++)
            {
                TimeLineFunctionControl titem = Children[i] as TimeLineFunctionControl;
                if (titem != null)
                    titem.ContentTemplate = dataTemplate;
            }
        }

      #region Conversion Utilities
      public Double ConvertTimeToDistance(TimeSpan span)
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

      public TimeSpan ConvertDistanceToTime(Double distance)
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
      }
      #endregion

      private void InitializeFunctions(ObservableCollection<ITimeLineDataItem> observableCollection)
        {
            if (observableCollection == null)
                return;
            this.Children.Clear();
            Children.Add(_gridCanvas);

         foreach (ITimeLineDataItem data in observableCollection)
            {
                TimeLineFunctionControl adder = CreateTimeLineFunctionControl(data);

                FunctionEdgeTrigger += adder.OnTrigger;

                Children.Add(adder);
            }
            Functions.CollectionChanged -= Items_CollectionChanged;
            Functions.CollectionChanged += Items_CollectionChanged;

         Children.Add(_positionIndicator);
      }

      void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                /* RPUGLIESE - Update/Initialise the list of Functions */
                OnFunctionsChanged(this, new DependencyPropertyChangedEventArgs(FunctionsProperty, null, Functions));

                var itm = e.NewItems[0] as ITimeLineDataItem;
                if (itm.StartTime.HasValue && itm.StartTime.Value == TimeSpan.MinValue)
                {//newly created item isn't a drop in so we need to instantiate and place its control.
                    TimeSpan duration = itm.EndTime.Value.Subtract(itm.StartTime.Value);
                    if (Functions.Count == 1)//this is the first one added
                    {
                        itm.StartTime = StartTime;
                        itm.EndTime = StartTime.Add(duration);
                    }
                    else
                    {
                        var last = Functions.OrderBy(i => i.StartTime.Value).LastOrDefault();
                        if (last != null)
                        {
                            itm.StartTime = last.EndTime;
                            itm.EndTime = itm.StartTime.Value.Add(duration);
                        }
                    }
                    var ctrl = CreateTimeLineFunctionControl(itm);
                    //The index if Items.Count-1 because of zero indexing.
                    //however our children is 1 indexed because 0 is our canvas grid.
                    Children.Insert(Functions.Count, ctrl);
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                var removeItem = e.OldItems[0];
                for (int i = 1; i < Children.Count; i++)
                {
                    TimeLineFunctionControl checker = Children[i] as TimeLineFunctionControl;
                    if (checker != null && checker.DataContext == removeItem)
                    {
                        Children.Remove(checker);
                        break;
                    }
                }
            }
        }


      /// <summary>
      /// Binds the ITimeLineDataItem to the TimeLineFunctionControl.
      /// </summary>
      /// <param name="data"></param>
      /// <returns></returns>
      private TimeLineFunctionControl CreateTimeLineFunctionControl(ITimeLineDataItem data)
      {
         Binding startBinding = new Binding("StartTime");
         startBinding.Mode = BindingMode.TwoWay;
         startBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;

         Binding endBinding = new Binding("EndTime");
         endBinding.Mode = BindingMode.TwoWay;
         endBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
         TimeSpan timelineStart = StartTime;

         Binding expandedBinding = new Binding("TimelineViewExpanded");
         expandedBinding.Mode = BindingMode.TwoWay;
         endBinding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;

         TimeLineFunctionControl timeLineFunctionCtrl = new TimeLineFunctionControl();
         timeLineFunctionCtrl.TimeLineStartTime = timelineStart;
         timeLineFunctionCtrl.DataContext = data;
         timeLineFunctionCtrl.Content = data;

         timeLineFunctionCtrl.SetBinding(TimeLineFunctionControl.StartTimeProperty, startBinding);
         timeLineFunctionCtrl.SetBinding(TimeLineFunctionControl.EndTimeProperty, endBinding);
         timeLineFunctionCtrl.SetBinding(TimeLineFunctionControl.IsExpandedProperty, expandedBinding);

         if (_template != null)
         {
            timeLineFunctionCtrl.ContentTemplate = _template;
         }

         /*adder.PreviewMouseLeftButtonDown += item_PreviewEditButtonDown;
         adder.MouseMove += item_MouseMove;
         adder.PreviewMouseLeftButtonUp += item_PreviewEditButtonUp;*/
         timeLineFunctionCtrl.PreviewMouseRightButtonDown += item_PreviewEditButtonDown;
         timeLineFunctionCtrl.MouseMove += item_MouseMove;
         timeLineFunctionCtrl.PreviewMouseRightButtonUp += item_PreviewEditButtonUp;

         timeLineFunctionCtrl.PreviewMouseLeftButtonUp += item_PreviewDragButtonUp;
         timeLineFunctionCtrl.PreviewMouseLeftButtonDown += item_PreviewDragButtonDown;
         timeLineFunctionCtrl.UnitSize = UnitSize;

         timeLineFunctionCtrl.MouseDoubleClick += TimeLineFunctionCtrl_MouseDoubleClick;

         return timeLineFunctionCtrl;
      }

      /// <summary>
      /// Mouse Double Click handling event.
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      private void TimeLineFunctionCtrl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
      {
         if (HostCanvas != null)
         {
            TimeLineControlFunctionSettingMenu settingMenuControl = new TimeLineControlFunctionSettingMenu();

            settingMenuControl.Style = (Style)FindResource("TimeLineFunctionControlSettingMenuStyle");
            settingMenuControl.Closed += SettingMenuControl_Closed;

            /* Set the position to the location of the mouse click event */
            Canvas.SetLeft(settingMenuControl, e.GetPosition((HostCanvas as IInputElement)).X);
            Canvas.SetTop(settingMenuControl, e.GetPosition((HostCanvas as IInputElement)).Y);

            HostCanvas.Children.Add(settingMenuControl);
         }
      }

      private void SettingMenuControl_Closed(object sender, EventArgs e)
      {
         HostCanvas.Children.Remove((sender as UIElement));
      }
      #endregion


      #region Position Line Indicator
      TimeLinePositionIndicator _positionIndicator;
      DispatcherTimer _timerDispatcher;
      public event EventHandler<FunctionTriggerEventArgs> FunctionEdgeTrigger;

      private void CheckPositionLine()
      {
         for (int i = 0; i < Children.Count; i++)
         {
            TimeLineFunctionControl titem = Children[i] as TimeLineFunctionControl;

            if (titem != null)
            {
               TimeSpan currentPosition = _positionIndicator.CurrentPosition.Elapsed;

               switch (titem.CurrentEdge)
               {
                  case FunctionTriggerEdge.Waiting:
                     if ((currentPosition >= titem.StartTime) && (currentPosition < titem.EndTime))
                     {
                        titem.CurrentEdge = FunctionTriggerEdge.Start;

                        if (FunctionEdgeTrigger != null)
                           FunctionEdgeTrigger.Invoke(titem, new FunctionTriggerEventArgs(currentPosition, FunctionTriggerEdge.Start));
                     }
                     break;
                  case FunctionTriggerEdge.Start:
                     if ((currentPosition < titem.StartTime) || (currentPosition >= titem.EndTime))
                     {
                        titem.CurrentEdge = FunctionTriggerEdge.End;

                        if (FunctionEdgeTrigger != null)
                           FunctionEdgeTrigger.Invoke(titem, new FunctionTriggerEventArgs(currentPosition, FunctionTriggerEdge.End));
                     }
                     break;
                  case FunctionTriggerEdge.End:
                  default:
                     titem.CurrentEdge = FunctionTriggerEdge.Waiting;
                     break;
               }
            }
         }
      }


      /// <summary>
      /// Handles updating of the Position Line Indicator.
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      private void TimerTick(object sender, EventArgs e)
      {
         if (_positionIndicator != null)
         {
            double position = _positionIndicator.CurrentPosition.ElapsedMilliseconds;

            OnTimerTick?.Invoke(this, new FunctionTriggerEventArgs(TimeSpan.FromMilliseconds(position), null));

            /* Have we reached the END of the TimeLineControl scale */
            if (position <= ConvertDistanceToTime(Width).TotalMilliseconds)
            {
               position = ConvertTimeToDistance(_positionIndicator.CurrentPosition.Elapsed);

               Canvas.SetLeft(_positionIndicator, position);
            }
            else
            {
               _timerDispatcher.Stop();
               _positionIndicator.CurrentPosition.Stop();
               Canvas.SetLeft(_positionIndicator, TimeSpan.MinValue.TotalMilliseconds);

               OnTimerTick?.Invoke(this, new FunctionTriggerEventArgs(TimeSpan.FromMilliseconds(position), FunctionTriggerEdge.End));
            }

            /* Check if we have reached START/END of a TimeLineFunctionControl */
            CheckPositionLine();
         }
      }

      private void SetPositionLineTemplate(ControlTemplate ctrlTemplate)
      {
         _positionLineTemplate = ctrlTemplate;
         for (int i = 0; i < Children.Count; i++)
         {
            TimeLinePositionIndicator titem = Children[i] as TimeLinePositionIndicator;
            if (titem != null)
               titem.Template = ctrlTemplate;
         }
      }

      /// <summary>
      /// If required, will create the Position Line Indicator and place it on the Canvas.
      /// </summary>
      public void InitializePositionLineIndicator()
      {
         if (_positionIndicator == null)
         {
            _positionIndicator = CreateTimeLinePositionIndicatorControl();

            Children.Add(_positionIndicator);

            Canvas.SetLeft(_positionIndicator, TimeSpan.MinValue.Ticks);
         }
      }

      /// <summary>
      /// Triggers the START of the Position Line Indicator.
      /// </summary>
      public void StartPositionLineIndication()
      {
         if (_positionIndicator != null)
         {
            Canvas.SetLeft(_positionIndicator, 0);

            _timerDispatcher = new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Normal, TimerTick, Dispatcher.CurrentDispatcher);
            _timerDispatcher.Start();

            _positionIndicator.CurrentPosition.Restart();

            CheckPositionLine();
         }
      }


      /// <summary>
      /// Creates the PositionLineIndicatorControl, assigning any Templates and Properties.
      /// </summary>
      /// <returns>Initialised PositionLineIndicatorControl object.</returns>
      private TimeLinePositionIndicator CreateTimeLinePositionIndicatorControl()
      {
         TimeLinePositionIndicator timeLinePositionIndicatorCtrl = new TimeLinePositionIndicator();

         if (_positionLineTemplate != null)
         {
            timeLinePositionIndicatorCtrl.Template = _positionLineTemplate;
         }

         timeLinePositionIndicatorCtrl.UnitSize = UnitSize;

         return timeLinePositionIndicatorCtrl;
      }
      #endregion

      #region updaters fired on dp changes
      private void UpdateUnitSize(double size)
      {
         /* If there are NO TimeLineFunctionControls, skip the resize of these */
         if (Functions != null)
         {
            /* Resize each TimeLineFunctionControl contained within the TimeLineControl */
            for (int i = 0; i < Functions.Count; i++)
            {
               TimeLineFunctionControl titem = GetTimeLineFunctionControlAt(i);
               if (titem != null)
                  titem.UnitSize = size;
            }
         }

         if(_positionIndicator != null)
         {
            _positionIndicator.UnitSize = size;
         }
         
         /* Redraw Children such as the Grid */
         ReDrawChildren();
      }


        //TODO: set up the timeline start date dependency property and do this margin check
        //for all including the first one.
        private void ReDrawChildren()
        {
            if (Functions == null)
            {
                DrawBackGround();
                return;
            }
         TimeSpan start = (TimeSpan)GetValue(StartTimeProperty);
            Double w = 0;
            Double s = 0;
            Double e = 0;
            for (int i = 0; i < Functions.Count; i++)
            {
                var mover = GetTimeLineFunctionControlAt(i);
                if (mover != null)
                {
                    mover.TimeLineStartTime = start;

                    if (!mover.ReadyToDraw)
                        mover.ReadyToDraw = true;

                    mover.PlaceOnCanvas();

                    mover.GetPlacementInfo(ref s, ref w, ref e);
                }

            }
            //find our background rectangle and set its width;
            DrawBackGround();
        }
      #endregion

      #region background and grid methods
      /// <summary>
      /// Handles drawing of the background (with/without grid)
      /// </summary>
      /// <param name="isDrawGridUpdate"></param>
      private void DrawBackGround(Boolean isDrawGridUpdate = false)
        {
            Brush b = Background;
            double setWidth = MinWidth;
            if (_gridCanvas.Children.Count <= 0)
            {
                _gridCanvas.Children.Add(new Rectangle());
            }
            Rectangle bg = _gridCanvas.Children[0] as Rectangle;
            if (!_startTimeInitialized ||
                !_unitSizeInitialized ||
                !_functionsInitialized ||
                Functions == null)
            {
                setWidth = Math.Max(MinWidth, GetMyWidth());
                setWidth = Math.Max(setWidth, SynchWidth);
                bg.Width = setWidth;
                bg.Height = Math.Max(DesiredSize.Height, Height);
                if (Double.IsNaN(bg.Height) || bg.Height < MinHeight)
                {
                    bg.Height = MinHeight;
                }
                bg.Fill = b;
                Width = bg.Width;
                Height = bg.Height;

            }
            else
            {
                var oldW = Width;
                var oldDrawTimeGrid = DrawTimeGrid;
                if (isDrawGridUpdate)
                    oldDrawTimeGrid = !oldDrawTimeGrid;
                //this is run every time we may need to update our siblings.
                SynchronizeSiblings();

                if (Functions == null)
                    return;
                setWidth = Math.Max(MinWidth, GetMyWidth());
                setWidth = Math.Max(setWidth, SynchWidth);
                bg.Width = setWidth;
                bg.Height = Math.Max(DesiredSize.Height, Height);
                if (Double.IsNaN(bg.Height) || bg.Height < MinHeight)
                {
                    bg.Height = MinHeight;
                }
                bg.Fill = b;
                Width = bg.Width;
                Height = bg.Height;
                if (DrawTimeGrid)
                {
                    if (Width != oldW || !oldDrawTimeGrid || (Width == MinWidth))
                        DrawTimeGridExecute();
                }
                else
                {
                    ClearTimeGridExecute();
                }
                if ((oldW != Width) && (_scrollViewer != null))//if we are at min width then we need to redraw our time grid when unit sizes change
                {
                    var available = LayoutInformation.GetLayoutSlot(_scrollViewer);
                    Size s = new Size(available.Width, available.Height);
                    _scrollViewer.Measure(s);
                    _scrollViewer.Arrange(available);
                }
            }
        }

        internal Double GetMyWidth()
        {
            if (Functions == null)
            {
                return MinWidth;
            }
            var lastItem = GetTimeLineFunctionControlAt(Functions.Count - 1);

            if (lastItem == null)
                return MinWidth;
            Double l = 0;
            Double w = 0;
            Double e = 0;
            lastItem.GetPlacementInfo(ref l, ref w, ref e);
            return Math.Max(MinWidth, e);
        }
        private void SynchronizeSiblings()
        {
            if (!SynchedWithSiblings)
                return;
            var current = VisualTreeHelper.GetParent(this) as FrameworkElement;

            while (current != null && !(current is ItemsControl))
            {
                current = VisualTreeHelper.GetParent(current) as FrameworkElement;
            }

            if (current is ItemsControl)
            {
                var pnl = current as ItemsControl;
                //this is called on updates for all siblings so it could easily
                //end up infinitely looping if each time tried to synch its siblings
                Boolean isSynchInProgress = false;
                //is there a synch instigator
                Double maxWidth = GetMyWidth();

                var siblings = TimeLineControl.FindAllTimeLineControlsInsidePanel(current);

                foreach (var ctrl in siblings)
                {
                    var tcSib = ctrl as TimeLineControl;
                    if (tcSib != null)
                    {
                        if (tcSib._isSynchInstigator)
                            isSynchInProgress = true;
                        Double sibW = tcSib.GetMyWidth();
                        if (sibW > maxWidth)
                        {
                            maxWidth = sibW;
                        }
                    }

                }
                SynchWidth = maxWidth;
                if (!isSynchInProgress)
                {
                    _isSynchInstigator = true;
                    foreach (var ctrl in siblings)
                    {
                        var tcSib = ctrl as TimeLineControl;
                        if (tcSib != null && tcSib != this)
                        {
                            tcSib.SynchWidth = maxWidth;
                            //tcSib.UnitSize = UnitSize;
                            //tcSib.StartDate = StartDate;
                            tcSib.DrawBackGround();
                        }
                    }
                }
                _isSynchInstigator = false;
            }
        }
        //helper to let a panel find all children of a given type
        private static IEnumerable<TimeLineControl> FindAllTimeLineControlsInsidePanel(DependencyObject depObj)
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is TimeLineControl)
                    {
                        yield return (TimeLineControl)child;
                    }

                    foreach (TimeLineControl childOfChild in FindAllTimeLineControlsInsidePanel(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void ClearTimeGridExecute()
        {
            if (_gridCanvas.Children.Count == 2)
                _gridCanvas.Children.RemoveAt(1);
        }

      private void DrawTimeGridExecute()
      {
         /* No items contained in the TimeLineControl */
         if (Functions == null)
               return;
         /* StartTime is at DEFAULT value */
         if (StartTime == TimeSpan.MinValue)
               return;
         if (_gridCanvas.Children.Count < 2)
         {
               _gridCanvas.Children.Add(new Canvas());
         }
         Canvas gridCanvas = _gridCanvas.Children[1] as Canvas;
         gridCanvas.Children.Clear();
         //Double hourSize = UnitSize;

         //place our gridlines
         if (ViewLevel >= TimeLineViewLevel.Days)
            DrawDayLines(gridCanvas);
         else if (ViewLevel >= TimeLineViewLevel.Hours)
            DrawHourLines(gridCanvas);
         else if (ViewLevel >= TimeLineViewLevel.Minutes)
            DrawMinuteLines(gridCanvas);
         else if (ViewLevel >= TimeLineViewLevel.Seconds)
            DrawSecondLines(gridCanvas);
         else if (ViewLevel >= TimeLineViewLevel.MilliSeconds)
            DrawMilliSecondLines(gridCanvas);
         TextBlock tScale = new TextBlock()
         {
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 9
         };
         gridCanvas.Children.Add(tScale);
         Canvas.SetLeft(tScale, 0);
         switch (ViewLevel)
         {
            case TimeLineViewLevel.MilliSeconds:
               tScale.Text = "Milliseconds";
               break;
            case TimeLineViewLevel.Seconds:
               tScale.Text = "Seconds";
               break;
            case TimeLineViewLevel.Minutes:
               tScale.Text = "Minutes";
               break;
            case TimeLineViewLevel.Hours:
               tScale.Text = "Hours";
               break;
            case TimeLineViewLevel.Days:
               tScale.Text = "Days";
               break;
            case TimeLineViewLevel.Weeks:
               tScale.Text = "Weeks";
               break;
            case TimeLineViewLevel.Months:
               tScale.Text = "Months";
               break;
            case TimeLineViewLevel.Years:
               tScale.Text = "Years";
               break;
         }
      }

      private void DrawMilliSecondLines(Canvas grid)
      {
         Double milliSecondSize = UnitSize;
         Double tenMilliSecondSize = UnitSize * 10;
         Double oneHundredMilliSecondSize = UnitSize * 100;
         int startMilliSecond = StartTime.Milliseconds;
         int startSecond = StartTime.Seconds;
         int remainingMilliSeconds = 1 - startMilliSecond;
         if (remainingMilliSeconds == 1)
            remainingMilliSeconds = 0;

         if (startMilliSecond != 0)
            remainingMilliSeconds--;
         else remainingMilliSeconds = 0;

         TimeSpan nextGap = new TimeSpan(0, 0, 0, 0, remainingMilliSeconds);
         TimeSpan nextTime = StartTime.Add(nextGap);
         Double nextDistance = nextGap.TotalHours * UnitSize;

         if (milliSecondSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 0, 0, 1), milliSecondSize, SecondLineBrush, 0);
         }
         else if(tenMilliSecondSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 0, 0, 10), tenMilliSecondSize, SecondLineBrush, 0);
         }
         else if (oneHundredMilliSecondSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 0, 0, 100), oneHundredMilliSecondSize, SecondLineBrush, 0);
         }
      }
      private void DrawSecondLines(Canvas grid)
      {
         Double MinuteSize = UnitSize * 60;
         Double halfMinuteSize = UnitSize * 30;
         Double secondSize = UnitSize;
         Double halfSecondSize = UnitSize / 2;
         int startSecond = StartTime.Seconds;
         int startMinute = StartTime.Minutes;
         int remainingSeconds = 60 - startSecond;
         if (remainingSeconds == 60)
            remainingSeconds = 0;

         if (startSecond < 45)
            remainingSeconds = 45 - startSecond;
         if (startSecond < 30)
            remainingSeconds = 30 - startSecond;
         if (startSecond < 15)
            remainingSeconds = 15 - startSecond;
         if (startSecond != 0)
            remainingSeconds--;
         else remainingSeconds = 0;

         TimeSpan nextGap = new TimeSpan(0, 0, remainingSeconds);
         TimeSpan nextTime = StartTime.Add(nextGap);
         Double nextDistance = nextGap.TotalHours * UnitSize;

         if (halfSecondSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 0, 0, 500), halfSecondSize, SecondLineBrush, 0);
         }
         else if (secondSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 0, 1), secondSize, SecondLineBrush, 0);
         }
         else if (halfMinuteSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 0, 30), halfMinuteSize, SecondLineBrush, 0);
         }
         else if (MinuteSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 1, 0), MinuteSize, SecondLineBrush, 0);
         }
      }
      private void DrawMinuteLines(Canvas grid)
      {
         Double halfHourSize = UnitSize / 2;
         Double fifteenMinSize = UnitSize / 4;
         Double minuteSize = UnitSize / 60;
         int startMinute = StartTime.Minutes;
         int startSecond = StartTime.Seconds;
         int remainingMinutes = 60 - startMinute;
         int remainingSeconds = 60 - startSecond;
         if (remainingSeconds == 60)
               remainingSeconds = 0;

         if (startMinute < 45)
            remainingMinutes = 45 - startMinute;
         if (startMinute < 30)
            remainingMinutes = 30 - startMinute;
         if (startMinute < 15)
            remainingMinutes = 15 - startMinute;
         if (startSecond != 0)
            remainingMinutes--;
         else remainingMinutes = 0;

         TimeSpan nextGap = new TimeSpan(0, remainingMinutes, remainingSeconds);
         TimeSpan nextTime = StartTime.Add(nextGap);
         Double nextDistance = nextGap.TotalHours * UnitSize;

         if (minuteSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 1, 0), minuteSize, MinuteLineBrush, 0);
         }
         else if (fifteenMinSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 15, 0), fifteenMinSize, MinuteLineBrush, 0);
         }
         else if (halfHourSize >= MinimumUnitWidth)
         {
            DrawIncrementLines(grid, nextTime, nextDistance, new TimeSpan(0, 30, 0), halfHourSize, MinuteLineBrush, 0);
         }
      }
      private void DrawHourLines(Canvas grid)
      {
         Double hourSize = Math.Abs(UnitSize);
         Double halfDaySize = Math.Abs(hourSize * 12);
         int startMinute = StartTime.Minutes;
         int remainingMinutes = 60 - startMinute;
         int startSecond = StartTime.Seconds;
         int remainingSeconds = 60 - startSecond;
         if (remainingSeconds == 60)
               remainingSeconds = 0;
         if (startSecond != 0)
               remainingMinutes--;
         if (startSecond != 0)
               remainingMinutes--;
         else remainingMinutes = 0;


         if (hourSize >= MinimumUnitWidth)
         {
            int remainingToMajor = 24 - StartTime.Hours;
               if (StartTime.Hours < 12)
                  remainingToMajor = 12 - StartTime.Hours;
               //time to our next hour
               TimeSpan firstHourGap = new TimeSpan(0, remainingMinutes, remainingSeconds);
         TimeSpan nextHour = StartTime.Add(firstHourGap);
               Double firstHourDistance = firstHourGap.TotalHours * hourSize;
               DrawIncrementLines(grid, nextHour, firstHourDistance, new TimeSpan(1, 0, 0), hourSize, HourLineBrush, 12, remainingToMajor);
         }
         else if (halfDaySize >= MinimumUnitWidth)
         {
            int startHour = StartTime.Hours;
               int remainingHours = 24 - startHour;
               if (startHour < 12)
               {
                  remainingHours = 12 - startHour;
               }
               if (startMinute != 0)
                  remainingHours--;


               TimeSpan nextHalfGap = new TimeSpan(remainingHours, remainingMinutes, remainingSeconds);
         TimeSpan nextHalfDay = StartTime.Add(nextHalfGap);
               Double nextHalfDistance = nextHalfGap.TotalHours * hourSize;
               DrawIncrementLines(grid, nextHalfDay, nextHalfDistance, new TimeSpan(12, 0, 0), halfDaySize, HourLineBrush, -1);

         }
      }
      private void DrawDayLines(Canvas grid)
      {
         Double daySize = UnitSize * 24;

         if (daySize >= MinimumUnitWidth)
         {
            TimeSpan increment = new TimeSpan(24, 0, 0);
               int startHour = StartTime.Hours;
               int startMinute = StartTime.Minutes;
               int remainingHours = 24 - startHour;
               if (startMinute > 0)
                  remainingHours--;
               int remainingMinutes = 60 - startMinute;
               if (startMinute == 0)
                  remainingMinutes = 0;
               int startSecond = StartTime.Seconds;
               int remainingSeconds = 60 - startSecond;
               if (startSecond != 0)
                  remainingMinutes--;
               else
                  remainingSeconds = 0;

            TimeSpan firstDayGap = new TimeSpan(remainingHours, remainingMinutes, remainingSeconds);
               Double firstDayDistance = (firstDayGap.TotalHours * UnitSize);
         TimeSpan nextDay = StartTime.Add(new TimeSpan(remainingHours, remainingMinutes, 0));


               DrawIncrementLines(grid, nextDay, firstDayDistance, increment, daySize, DayLineBrush, 7);
         }

      }
      private void DrawIncrementLines(Canvas grid, TimeSpan firstLineTime, Double firstLineDistance, TimeSpan timeStep, Double unitSize, Brush brush, int majorEvery, int majorEveryOffset = 0)
      {
         Double curX = firstLineDistance;
         TimeSpan curTime = firstLineTime;
         int curLine = 0;

         while (curX < Width)
            {
                Line l = new Line();
                l.ToolTip = curTime;
                l.StrokeThickness = MinorUnitThickness;
                if ((majorEvery > 0) && ((curLine - majorEveryOffset) % majorEvery == 0))
                {
                    l.StrokeThickness = MajorUnitThickness;
                }
                l.Stroke = brush;
                l.X1 = 0;
                l.X2 = 0;
                l.Y1 = 0;
                l.Y2 = Math.Max(DesiredSize.Height, Height);
            grid.Children.Add(l);
            Canvas.SetLeft(l, curX);

            TextBlock t = new TextBlock()
            {
               Margin = new Thickness(0, -2, 0, 0),
               FontSize = 9
            };

            grid.Children.Add(t);
            Canvas.SetLeft(t, curX);

            switch (ViewLevel)
            {
               case TimeLineViewLevel.MilliSeconds:
                  t.Text = Math.Round(curTime.TotalMilliseconds, 2).ToString();
                  break;
               case TimeLineViewLevel.Seconds:
                  t.Text = Math.Round(curTime.TotalSeconds, 2).ToString();
                  break;
               case TimeLineViewLevel.Minutes:
                  t.Text = Math.Round(curTime.TotalMinutes, 2).ToString();
                  break;
               case TimeLineViewLevel.Hours:
                  t.Text = Math.Round(curTime.TotalHours, 2).ToString();
                  break;
               case TimeLineViewLevel.Days:
                  t.Text = Math.Round(curTime.TotalDays, 2).ToString();
                  break;
               case TimeLineViewLevel.Weeks:
                  t.Text = Math.Round((curTime.TotalDays / 7.0), 2).ToString();
                  break;
               case TimeLineViewLevel.Months:
                  t.Text = Math.Round((curTime.TotalDays / 30.0), 2).ToString();
                  break;
               case TimeLineViewLevel.Years:
                  t.Text = Math.Round((curTime.TotalDays / 365.0), 2).ToString();
                  break;
            }

            curX += unitSize;
                curTime += timeStep;
                curLine++;
            }
        }
        #endregion

        #region mouse enter and leave events
        void TimeLineControl_MouseLeave(object sender, MouseEventArgs e)
        {
            //Keyboard.Focus(this);
        }

        void TimeLineControl_MouseEnter(object sender, MouseEventArgs e)
        {
            //Keyboard.Focus(this);
        }
        #endregion

        #region drag events and fields
        private Boolean _dragging = false;
        private Point _dragStartPosition = new Point(double.MinValue, double.MinValue);
        /// <summary>
        /// When we drag something from an external control over this I need a temp control
        /// that lets me adorn those accordingly as well
        /// </summary>
        private TimeLineFunctionControl _tmpDraggAdornerControl;

        TimeLineFunctionControl _dragObject = null;
        void item_PreviewDragButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPosition = Mouse.GetPosition(null);
            _dragObject = sender as TimeLineFunctionControl;

        }

        void item_PreviewDragButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPosition.X = double.MinValue;
            _dragStartPosition.Y = double.MinValue;
            _dragObject = null;
        }


        void TimeLineControl_DragOver(object sender, DragEventArgs e)
        {
            //throw new NotImplementedException();
            TimeLineFunctionControl d = e.Data.GetData(typeof(TimeLineFunctionControl)) as TimeLineFunctionControl;

            if (d != null)
            {
                if (Manager != null)
                {
                    if (!Manager.CanAddToTimeLine(d.DataContext as ITimeLineDataItem))
                    {
                        e.Effects = DragDropEffects.None;
                        return;
                    }
                }
                e.Effects = DragDropEffects.Move;
                //this is an internal drag or a drag from another time line control
                if (DragAdorner == null)
                {
                    _dragAdorner = new TimeLineDragAdorner(d, FunctionTemplate);

                }
                DragAdorner.MousePosition = e.GetPosition(d);
                DragAdorner.InvalidateVisual();

            }
            else
            {//GongSolutions.Wpf.DragDrop

                var d2 = e.Data.GetData("GongSolutions.Wpf.DragDrop");
                if (d2 != null)
                {
                    if (Manager != null)
                    {
                        if (!Manager.CanAddToTimeLine(d2 as ITimeLineDataItem))
                        {
                            e.Effects = DragDropEffects.None;
                            return;
                        }
                    }

                    e.Effects = DragDropEffects.Move;
                    if (DragAdorner == null)
                    {
                        //we are dragging from an external source and we don't have a timeline item control of any sort
                        Children.Remove(_tmpDraggAdornerControl);
                        //in order to get an adornment layer the control has to be somewhere
                        _tmpDraggAdornerControl = new TimeLineFunctionControl();
                        _tmpDraggAdornerControl.UnitSize = UnitSize;
                        Children.Add(_tmpDraggAdornerControl);
                        Canvas.SetLeft(_tmpDraggAdornerControl, -1000000);
                        _tmpDraggAdornerControl.DataContext = d2;
                        _tmpDraggAdornerControl.StartTime = StartTime;
                        _tmpDraggAdornerControl.InitializeDefaultLength();
                        _tmpDraggAdornerControl.ContentTemplate = FunctionTemplate;

                        _dragAdorner = new TimeLineDragAdorner(_tmpDraggAdornerControl, FunctionTemplate);
                    }
                    DragAdorner.MousePosition = e.GetPosition(_tmpDraggAdornerControl);
                    DragAdorner.InvalidateVisual();
                }
            }
            DragScroll(e);


        }

        void TimeLineControL_DragLeave(object sender, DragEventArgs e)
        {
            DragAdorner = null;
            Children.Remove(_tmpDraggAdornerControl);
            _tmpDraggAdornerControl = null;
        }

   void TimeLineControl_Drop(object sender, DragEventArgs e)
   {
      DragAdorner = null;

      TimeLineFunctionControl dropper = e.Data.GetData(typeof(TimeLineFunctionControl)) as TimeLineFunctionControl;
      ITimeLineDataItem dropData = null;
      if (dropper == null)
      {
         //dropData = e.Data.GetData(typeof(ITimeLineDataItem)) as ITimeLineDataItem;
         dropData = e.Data.GetData("GongSolutions.Wpf.DragDrop") as ITimeLineDataItem;
         if (dropData != null)
         {
            //I haven't figured out why but
            //sometimes when dropping from an external source
            //the drop event hits twice.
            //that results in ugly duplicates ending up in the timeline
            //and it is a mess.

            if (Functions == null)
               Functions = new ObservableCollection<ITimeLineDataItem>();

            if (Functions.Contains(dropData))
               return;
                  //create a new timeline item control from this data
                  dropper = CreateTimeLineFunctionControl(dropData);
                  dropper.StartTime = StartTime;
                  dropper.InitializeDefaultLength();
                  Children.Remove(_tmpDraggAdornerControl);
                  _tmpDraggAdornerControl = null;

               }
         }
         var dropX = e.GetPosition(this).X;
         int newIndex = GetDroppedNewIndex(dropX);
         var curData = dropper.DataContext as ITimeLineDataItem;
         var curIndex = Functions.IndexOf(curData);
         if ((curIndex == newIndex || curIndex + 1 == newIndex) && dropData == null && dropper.Parent == this)//dropdata null is to make sure we aren't failing on adding a new data item into the timeline
         //dropper.parent==this makes it so that we allow a dropper control from another timeline to be inserted in at the start.
         {
               return;//our drag did nothing meaningful so we do nothing.
         }

         if (dropper != null)
         {
            TimeSpan start = (TimeSpan)GetValue(StartTimeProperty);
            if (newIndex == 0)
            {
               if (dropData == null)
               {
                  RemoveTimeLineItemControl(dropper);
               }
               if (dropper.Parent != this && dropper.Parent is TimeLineControl)
               {
                  var tlCtrl = dropper.Parent as TimeLineControl;
                  tlCtrl.RemoveTimeLineItemControl(dropper);
               }
               InsertTimeLineItemControlAt(newIndex, dropper);
               dropper.MoveToNewStartTime(start);
               MakeRoom(newIndex, dropper.Width);


            }
            else//we are moving this after something.
            {

               //find out if we are moving the existing one back or forward.
               var placeAfter = GetTimeLineFunctionControlAt(newIndex - 1);
               if (placeAfter != null)
               {
                  start = placeAfter.EndTime;
                  RemoveTimeLineItemControl(dropper);
                  if (curIndex < newIndex && curIndex >= 0)//-1 is on an insert in which case we definitely don't want to take off on our new index value
                  {
                        //we are moving forward.
                        newIndex--;//when we removed our item, we shifted our insert index back 1
                  }
                  if (dropper.Parent != null && dropper.Parent != this)
                  {
                        var ptl = dropper.Parent as TimeLineControl;
                        ptl.RemoveTimeLineItemControl(dropper);
                  }

                  InsertTimeLineItemControlAt(newIndex, dropper);
                  dropper.MoveToNewStartTime(start);
                  MakeRoom(newIndex, dropper.Width);
               }
            }
         }
         //ReDrawChildren();
         DrawBackGround();
         e.Handled = true;
      }


        #region drop helpers
        private void InsertTimeLineItemControlAt(int index, TimeLineFunctionControl adder)
        {
            var Data = adder.DataContext as ITimeLineDataItem;
            if (Functions.Contains(Data))
                return;

            adder.PreviewMouseRightButtonDown -= item_PreviewEditButtonDown;
            adder.MouseMove -= item_MouseMove;
            adder.PreviewMouseRightButtonUp -= item_PreviewEditButtonUp;

            adder.PreviewMouseLeftButtonUp -= item_PreviewDragButtonUp;
            adder.PreviewMouseLeftButtonDown -= item_PreviewDragButtonDown;

            adder.PreviewMouseRightButtonDown += item_PreviewEditButtonDown;
            adder.MouseMove += item_MouseMove;
            adder.PreviewMouseRightButtonUp += item_PreviewEditButtonUp;

            adder.PreviewMouseLeftButtonUp += item_PreviewDragButtonUp;
            adder.PreviewMouseLeftButtonDown += item_PreviewDragButtonDown;
            //child 0 is our grid and we want to keep that there.
            Children.Insert(index + 1, adder);
            Functions.Insert(index, Data);
        }
        private void RemoveTimeLineItemControl(TimeLineFunctionControl remover)
        {
            var curData = remover.DataContext as ITimeLineDataItem;
            remover.PreviewMouseRightButtonDown -= item_PreviewEditButtonDown;
            remover.MouseMove -= item_MouseMove;
            remover.PreviewMouseRightButtonUp -= item_PreviewEditButtonUp;

            remover.PreviewMouseLeftButtonUp -= item_PreviewDragButtonUp;
            remover.PreviewMouseLeftButtonDown -= item_PreviewDragButtonDown;
            Functions.Remove(curData);
            Children.Remove(remover);
        }
        private int GetDroppedNewIndex(Double dropX)
        {
            Double s = 0;
            Double w = 0;
            Double e = 0;
            for (int i = 0; i < Functions.Count(); i++)
            {
                var checker = GetTimeLineFunctionControlAt(i);
                if (checker == null)
                    continue;
                checker.GetPlacementInfo(ref s, ref w, ref e);
                if (dropX < s)
                {
                    return i;
                }
                if (s < dropX && e > dropX)
                {
                    Double distStart = Math.Abs(dropX - s);
                    Double distEnd = Math.Abs(dropX - e);
                    if (distStart < distEnd)//we dropped closer to the start of this item
                    {
                        return i;
                    }
                    //we are closer to the end of this item
                    return i + 1;
                }
                if (e < dropX && i == Functions.Count() - 1)
                {
                    return i + 1;
                }
                if (s < dropX && i == Functions.Count() - 1)
                {
                    return i;
                }
            }
            return Functions.Count;

        }
        private void MakeRoom(int newIndex, Double width)
        {
            int moveIndex = newIndex + 1;
            //get our forward chain and gap
            Double chainGap = 0;

            //because the grid is child 0 and we are essentially indexing as if it wasn't there
            //the child index of add after is our effective index of next
            var nextCtrl = GetTimeLineFunctionControlAt(moveIndex);
            if (nextCtrl != null)
            {
                Double nL = 0;
                Double nW = 0;
                Double nE = 0;
                nextCtrl.GetPlacementInfo(ref nL, ref nW, ref nE);

                Double droppedIntoSpace = 0;
                if (newIndex == 0)
                {
                    droppedIntoSpace = nL;
                }
                else
                {
                    var previousControl = GetTimeLineFunctionControlAt(newIndex - 1);
                    if (previousControl != null)
                    {
                        Double aL = 0;
                        Double aW = 0;
                        Double aE = 0;
                        previousControl.GetPlacementInfo(ref aL, ref aW, ref aE);
                        droppedIntoSpace = nL - aE;
                    }
                }
                Double neededSpace = width - droppedIntoSpace;
                if (neededSpace <= 0)
                    return;

                var forwardChain = GetTimeLineForwardChain(nextCtrl, moveIndex + 1, ref chainGap);

                if (chainGap < neededSpace)
                {
                    while (neededSpace > 0)
                    {
                        //move it to the smaller of our values -gap or remaning space
                        Double move = Math.Min(chainGap, neededSpace);
                        foreach (var tictrl in forwardChain)
                        {
                            tictrl.MoveMe(move);
                            neededSpace -= move;
                        }
                        //get our new chain and new gap
                        forwardChain = GetTimeLineForwardChain(nextCtrl, moveIndex + 1, ref chainGap);
                    }
                }
                else
                {
                    foreach (var tictrl in forwardChain)
                    {
                        tictrl.MoveMe(neededSpace);
                    }
                }

            }//if next ctrl is null we are adding to the very end and there is no work to do to make room.
        }
        #endregion




        //NOT WORKING YET AND I DON'T KNOW WHY 8(
        private void DragScroll(DragEventArgs e)
        {
            if (_scrollViewer == null)
            {
                _scrollViewer = GetParentScrollViewer();
            }
            if (_scrollViewer != null)
            {
                var available = LayoutInformation.GetLayoutSlot(this);
                Point scrollPos = e.GetPosition(_scrollViewer);
                Double scrollMargin = 50;
                var actualW = _scrollViewer.ActualWidth;
                if (scrollPos.X >= actualW - scrollMargin &&
                    _scrollViewer.HorizontalOffset <= _scrollViewer.ExtentWidth - _scrollViewer.ViewportWidth)
                {
                    _scrollViewer.LineRight();
                }
                else if (scrollPos.X < scrollMargin && _scrollViewer.HorizontalOffset > 0)
                {
                    _scrollViewer.LineLeft();
                }
                Double actualH = _scrollViewer.ActualHeight;
                if (scrollPos.Y >= actualH - scrollMargin &&
                    _scrollViewer.VerticalOffset <= _scrollViewer.ExtentHeight - _scrollViewer.ViewportHeight)
                {
                    _scrollViewer.LineDown();
                }
                else if (scrollPos.Y < scrollMargin && _scrollViewer.VerticalOffset >= 0)
                {
                    _scrollViewer.LineUp();
                }
            }
        }



        #endregion


        #region edit events etc
        private Double _curX = 0;
        private TimeLineAction _action;
        void item_PreviewEditButtonUp(object sender, MouseButtonEventArgs e)
        {
            (sender as TimeLineFunctionControl).ReleaseMouseCapture();
            Keyboard.Focus(this);
        }

        void item_PreviewEditButtonDown(object sender, MouseButtonEventArgs e)
        {
            var ctrl = sender as TimeLineFunctionControl;

            _action = ctrl.GetClickAction();
            (sender as TimeLineFunctionControl).CaptureMouse();
        }



        #region key down and up
        Boolean _rightCtrlDown = false;
        Boolean _leftCtrlDown = false;
        protected void OnKeyDown(Object sender, KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                _rightCtrlDown = e.Key == Key.RightCtrl;
                _leftCtrlDown = e.Key == Key.LeftCtrl;
                ManipulationMode = TimeLineManipulationMode.Linked;
            }
        }
        protected void OnKeyUp(Object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl)
                _leftCtrlDown = false;
            if (e.Key == Key.RightCtrl)
                _rightCtrlDown = false;
            if (!_leftCtrlDown && !_rightCtrlDown)
                ManipulationMode = TimeLineManipulationMode.Linked;
        }

        internal void HandleItemManipulation(TimeLineFunctionControl ctrl, TimeLineItemChangedEventArgs e)
        {

            Boolean doStretch = false;
            TimeSpan deltaT = e.DeltaTime;
            TimeSpan zeroT = new TimeSpan();
            int direction = deltaT.CompareTo(zeroT);
            if (direction == 0)
                return;//shouldn't happen

            TimeLineFunctionControl previous = null;
            TimeLineFunctionControl after = null;
            int afterIndex = -1;
            int previousIndex = -1;
            after = GetTimeLineItemControlStartingAfter(ctrl.StartTime, ref afterIndex);
            previous = GetTimeLineItemControlStartingBefore(ctrl.StartTime, ref previousIndex);
            if (after != null)
                after.ReadyToDraw = false;
            if (ctrl != null)
                ctrl.ReadyToDraw = false;
            Double useDeltaX = e.DeltaX;
            Double cLeft = 0;
            Double cWidth = 0;
            Double cEnd = 0;
            ctrl.GetPlacementInfo(ref cLeft, ref cWidth, ref cEnd);

            switch (e.Action)
            {
                case TimeLineAction.Move:
                    #region move

                    Double chainGap = Double.MaxValue;
                    if (direction > 0)
                    {
                        //find chain connecteds that are after this one
                        //delta each one in that chain that we are pushing
                        List<TimeLineFunctionControl> afterChain = GetTimeLineForwardChain(ctrl, afterIndex, ref chainGap);

                        if (chainGap < useDeltaX)
                            useDeltaX = chainGap;
                        foreach (var ti in afterChain)
                        {
                            ti.MoveMe(useDeltaX);
                        }

                        //find the size of our chain so we bring it into view
                        var first = afterChain[0];
                        var last = afterChain[afterChain.Count - 1];
                        BringChainIntoView(first, last, direction);


                    }
                    if (direction < 0)
                    {
                        Boolean previousBackToStart = false;
                        List<TimeLineFunctionControl> previousChain = GetTimeLineBackwardsChain(ctrl, previousIndex, ref previousBackToStart, ref chainGap);
                        if (-chainGap > useDeltaX)
                        {
                            useDeltaX = chainGap;
                        }
                        if (!previousBackToStart)
                        {
                            foreach (var ti in previousChain)
                            {
                                ti.MoveMe(useDeltaX);
                            }
                        }
                        var first = previousChain[0];//previousChain[previousChain.Count - 1];
                        var last = previousChain[previousChain.Count - 1];
                        BringChainIntoView(last, first, direction);
                    }
                    #endregion
                    break;
                case TimeLineAction.StretchStart:
                    switch (e.Mode)
                    {
                        #region Stretch Start
                        case TimeLineManipulationMode.Linked:
                            #region Linked
                            Double gap = Double.MaxValue;
                            if (previous != null)
                            {
                                Double pLeft = 0;
                                Double pWidth = 0;
                                Double pEnd = 0;
                                previous.GetPlacementInfo(ref pLeft, ref pWidth, ref pEnd);
                                gap = cLeft - pEnd;
                            }
                            if (direction < 0 && Math.Abs(gap) < Math.Abs(useDeltaX) && Math.Abs(gap) > _bumpThreshold)//if we are negative and not linked, but about to bump
                                useDeltaX = -gap;
                            if (Math.Abs(gap) < _bumpThreshold)
                            {//we are linked
                                if (ctrl.CanDelta(0, useDeltaX) && previous.CanDelta(1, useDeltaX))
                                {
                                    ctrl.MoveStartTime(useDeltaX);
                                    previous.MoveEndTime(useDeltaX);
                                }
                            }
                            else if (ctrl.CanDelta(0, useDeltaX))
                            {
                                ctrl.MoveStartTime(useDeltaX);
                            }


                            break;
                            #endregion
                        case TimeLineManipulationMode.Free:
                            #region free
                            gap = Double.MaxValue;
                            doStretch = direction > 0;
                            if (direction < 0)
                            {
                                //disallow us from free stretching into another item

                                if (previous != null)
                                {
                                    Double pLeft = 0;
                                    Double pWidth = 0;
                                    Double pEnd = 0;
                                    previous.GetPlacementInfo(ref pLeft, ref pWidth, ref pEnd);
                                    gap = cLeft - pEnd;
                                }
                                else
                                {
                                    //don't allow us to stretch further than the gap between current and start time
                                    TimeSpan s = (TimeSpan)GetValue(StartTimeProperty);
                                    gap = cLeft;
                                }
                                doStretch = gap > _bumpThreshold;
                                if (gap < useDeltaX)
                                {
                                    useDeltaX = gap;
                                }
                            }

                            doStretch &= ctrl.CanDelta(0, useDeltaX);

                            if (doStretch)
                            {
                                ctrl.MoveStartTime(useDeltaX);
                            }
                            #endregion
                            break;
                        default:
                            break;
                        #endregion
                    }
                    break;
                case TimeLineAction.StretchEnd:
                    switch (e.Mode)
                    {
                        #region Stretch End
                        case TimeLineManipulationMode.Linked:
                            #region linked
                            Double gap = Double.MaxValue;
                            if (after != null)
                            {
                                Double aLeft = 0;
                                Double aWidth = 0;
                                Double aEnd = 0;
                                after.GetPlacementInfo(ref aLeft, ref aWidth, ref aEnd);
                                gap = aLeft - cEnd;
                            }

                            if (direction > 0 && gap > _bumpThreshold && gap < useDeltaX)//if we are positive, not linked but about to bump
                                useDeltaX = -gap;
                            if (gap < _bumpThreshold)
                            {//we are linked
                                if (ctrl.CanDelta(1, useDeltaX) && after.CanDelta(0, useDeltaX))
                                {
                                    ctrl.MoveEndTime(useDeltaX);
                                    after.MoveStartTime(useDeltaX);
                                }
                            }
                            else if (ctrl.CanDelta(0, useDeltaX))
                            {
                                ctrl.MoveEndTime(useDeltaX);
                            }
                            break;
                            #endregion
                        case TimeLineManipulationMode.Free:
                            #region free
                            Double nextGap = Double.MaxValue;
                            doStretch = true;
                            if (direction > 0 && after != null)
                            {
                                //disallow us from free stretching into another item
                                Double nLeft = 0;
                                Double nWidth = 0;
                                Double nEnd = 0;
                                after.GetPlacementInfo(ref nLeft, ref nWidth, ref nEnd);
                                nextGap = nLeft - cEnd;
                                doStretch = nextGap > _bumpThreshold;
                                if (nextGap < useDeltaX)
                                    useDeltaX = nextGap;
                            }


                            doStretch &= ctrl.CanDelta(1, useDeltaX);
                            if (doStretch)
                            {
                                ctrl.MoveEndTime(useDeltaX);
                            }

                            break;
                            #endregion
                        default:
                            break;
                        #endregion
                    }
                    break;
                default:
                    break;
            }
        }

        private void BringChainIntoView(TimeLineFunctionControl first, TimeLineFunctionControl last, int direction)
        {
            Double l1 = 0;
            Double l2 = 0;
            Double w = 0;
            Double w2 = 0;
            Double end = 0;
            first.GetPlacementInfo(ref l1, ref w, ref end);
            last.GetPlacementInfo(ref l2, ref w2, ref end);
            Double chainW = end - l1;
            Double leadBuffer = 4 * UnitSize;
            chainW += leadBuffer;
            if (direction > 0)
            {

                first.BringIntoView(new Rect(new Point(0, 0), new Point(chainW, Height)));
            }
            else
            {
                first.BringIntoView(new Rect(new Point(-leadBuffer, 0), new Point(chainW, Height)));
            }

        }

      #endregion
      #endregion

      /// <summary>
      /// Mouse move is important for both edit and drag behaviors
      /// </summary>
      /// <param name="sender"></param>
      /// <param name="e"></param>
      void item_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
      {
         #region drag - left click and move
         TimeLineFunctionControl ctrl = sender as TimeLineFunctionControl;
         if (ctrl == null)
            return;

         if (Mouse.LeftButton == MouseButtonState.Pressed)
         {
            if (ctrl.IsExpanded)
               return;
            var position = Mouse.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPosition.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPosition.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
               DragDrop.DoDragDrop(this, ctrl, DragDropEffects.Move | DragDropEffects.Scroll);
               _dragging = true;
            }

            return;
         }
         #endregion


         #region edits - right click and move
         if (Mouse.Captured != ctrl)
         {
            _curX = Mouse.GetPosition(null).X;
            return;
         }

         Double mouseX = Mouse.GetPosition(null).X;
         Double deltaX = Math.Truncate(mouseX - _curX);

         /* SnapTo UnitSize */
         if (SnapToGrid == true)
         {
            if ((deltaX >= (SnapToUnitSize * UnitSize)) || (deltaX <= -(SnapToUnitSize * UnitSize)))
            {
               deltaX = (int)(deltaX / (SnapToUnitSize * UnitSize)) * (SnapToUnitSize * UnitSize);
            }
            else
               return;
         }

         TimeSpan deltaT = ctrl.GetDeltaTime(deltaX);
         var curMode = (TimeLineManipulationMode)GetValue(ManipulationModeProperty);
         HandleItemManipulation(ctrl, new TimeLineItemChangedEventArgs()
         {
            Action = _action,
            DeltaTime = deltaT,
            DeltaX = deltaX,
            Mode = curMode
         });

         DrawBackGround();
         _curX = mouseX;

         //When we pressed, this lost focus and we therefore didn't capture any changes to the key status
         //so we check it again after our manipulation finishes.  That way we can be linked and go out of or back into it while dragging
         ManipulationMode = TimeLineManipulationMode.Free;
         _leftCtrlDown = Keyboard.IsKeyDown(Key.LeftCtrl);
         _rightCtrlDown = Keyboard.IsKeyDown(Key.RightCtrl);
         if (_leftCtrlDown || _rightCtrlDown)
         {
            ManipulationMode = TimeLineManipulationMode.Linked;
         }
         #endregion
      }



        #region get children methods

        /// <summary>
        /// Returns a list of all timeline controls starting with the current one and moving forward
        /// so long as they are contiguous.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private List<TimeLineFunctionControl> GetTimeLineForwardChain(TimeLineFunctionControl current, int afterIndex, ref Double chainGap)
        {
            List<TimeLineFunctionControl> returner = new List<TimeLineFunctionControl>() { current };
            Double left = 0, width = 0, end = 0;
            current.GetPlacementInfo(ref left, ref width, ref end);
            if (afterIndex < 0)
            {
                //we are on the end of the list so there is no limit.
                chainGap = Double.MaxValue;
                return returner;
            }
            Double bumpThreshold = _bumpThreshold;
            Double lastAddedEnd = end;
            while (afterIndex < Functions.Count)
            {
                left = width = end = 0;
                var checker = GetTimeLineFunctionControlAt(afterIndex++);
                if (checker != null)
                {
                    checker.GetPlacementInfo(ref left, ref width, ref end);
                    Double gap = left - lastAddedEnd;
                    if (gap > bumpThreshold)
                    {
                        chainGap = gap;
                        return returner;
                    }
                    returner.Add(checker);
                    lastAddedEnd = end;
                }

            }
            //we have chained off to the end and thus have no need to worry about our gap
            chainGap = Double.MaxValue;
            return returner;
        }

        /// <summary>
        /// Returns a list of all timeline controls starting with the current one and moving backwoards
        /// so long as they are contiguous.  If the chain reaches back to the start time of the timeline then the
        /// ChainsBackToStart boolean is modified to reflect that.
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private List<TimeLineFunctionControl> GetTimeLineBackwardsChain(TimeLineFunctionControl current, int prevIndex, ref Boolean ChainsBackToStart, ref Double chainGap)
        {
            List<TimeLineFunctionControl> returner = new List<TimeLineFunctionControl>() { current };
            Double left = 0, width = 0, end = 0;
            current.GetPlacementInfo(ref left, ref width, ref end);
            if (prevIndex < 0)
            {
                chainGap = Double.MaxValue;
                ChainsBackToStart = left == 0;
                return returner;
            }

            Double lastAddedLeft = left;
            while (prevIndex >= 0)
            {
                left = width = end = 0;

                var checker = GetTimeLineFunctionControlAt(prevIndex--);
                if (checker != null)
                {
                    checker.GetPlacementInfo(ref left, ref width, ref end);
                    if (lastAddedLeft - end > _bumpThreshold)
                    {
                        //our chain just broke;
                        chainGap = lastAddedLeft - end;
                        ChainsBackToStart = lastAddedLeft == 0;
                        return returner;
                    }
                    returner.Add(checker);
                    lastAddedLeft = left;
                }

            }
            ChainsBackToStart = lastAddedLeft == 0;
            chainGap = lastAddedLeft;//gap between us and zero;
            return returner;

        }

        private TimeLineFunctionControl GetTimeLineItemControlStartingBefore(TimeSpan dateTime, ref int index)
        {
            index = -1;
            for (int i = 0; i < Functions.Count; i++)
            {
                var checker = GetTimeLineFunctionControlAt(i);
                if (checker != null && checker.StartTime == dateTime && i != 0)
                {
                    index = i - 1;
                    return GetTimeLineFunctionControlAt(i - 1);
                }
            }
            index = -1;
            return null;
        }

        private TimeLineFunctionControl GetTimeLineItemControlStartingAfter(TimeSpan dateTime, ref int index)
        {
            for (int i = 0; i < Functions.Count; i++)
            {
                var checker = GetTimeLineFunctionControlAt(i);
                if (checker != null && checker.StartTime > dateTime)
                {
                    index = i;
                    return checker;
                }
            }
            index = -1;
            return null;
        }

        private TimeLineFunctionControl GetTimeLineFunctionControlAt(int i)
        {
            //child 0 is our background grid.
            i++;
            if (i <= 0 || i >= Children.Count)
                return null;
            return Children[i] as TimeLineFunctionControl;
        }

        #endregion
    }

}
