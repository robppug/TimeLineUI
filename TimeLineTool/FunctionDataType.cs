using System;
using System.ComponentModel;
using System.Windows.Media;

namespace TimeLineTool
{
   public class FunctionDataType : ITimeLineDataItem, INotifyPropertyChanged
   {
      public event PropertyChangedEventHandler PropertyChanged;

      private TimeSpan? _startTime;
      public TimeSpan? StartTime
      {
         get { return _startTime; }
         set { _startTime = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StartTime")); }
      }
      private TimeSpan? _endTime;
      public TimeSpan? EndTime
      {
         get { return _endTime; }
         set { _endTime = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("EndTime")); }
      }

      private Boolean _timelineViewExpanded;
      public Boolean TimelineViewExpanded
      {
         get { return _timelineViewExpanded; }
         set { _timelineViewExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TimelineViewExpanded")); }
      }

      private String _name;
      public String Name
      {
         get { return _name; }
         set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name")); }
      }

      private Brush _bgBrush;
      public Brush BgBrush
      {
         get { return _bgBrush; }
         set { _bgBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("BgBrush")); }
      }
   }
}
