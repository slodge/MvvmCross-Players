using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.OS;
using Cirrious.CrossCore.Converters;
using Cirrious.CrossCore.IoC;
using Cirrious.CrossCore.Platform;
using Cirrious.CrossCore.UI;
using Cirrious.MvvmCross.Binding;
using Cirrious.MvvmCross.Binding.Attributes;
using Cirrious.MvvmCross.Binding.Droid;
using Cirrious.MvvmCross.Binding.Droid.Target;
using Cirrious.MvvmCross.Binding.Droid.Views;
using Cirrious.MvvmCross.Droid.Platform;
using Cirrious.MvvmCross.Droid.Views;
using Cirrious.MvvmCross.Plugins.Color;
using Cirrious.MvvmCross.ViewModels;

namespace PointsOnGrid
{
    public class GridViewModel : MvxViewModel
    {
        public class Point
        {
            public string Player { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public MvxColor Color { get; set; }
        }

        public List<Point> Points { get; set; }

        public GridViewModel()
        {
            Points = new List<Point>();
            var r = new Random();
            for (var i = 0; i < 20; i++)
            {
                Points.Add(new Point()
                    {
                        X = r.Next(8) + 1, 
                        Y = r.Next(4) + 1,
                        Color = Color(r.Next(4)),
                        Player = Name(i)
                    });
            }
        }

        private static string Name(int which)
        {
            return "Player " + which;
        }

        private static MvxColor Color(int which)
        {
            switch (which)
            {
                case 0:
                    return new MvxColor(255,0,0);
                case 1:
                    return new MvxColor(0, 0, 255);
                case 2:
                    return new MvxColor(255, 0, 255);
                case 3:
                default:
                    return new MvxColor(255, 255, 0);
            }
        }
    }
    public class App
        : MvxApplication
    {
        public App()
        {
            Mvx.RegisterSingleton<IMvxAppStart>(new MvxAppStart<GridViewModel>());
        }
    }

    public class BaseGridConverter : MvxValueConverter
    {
        public static int ScreenX { get; set; }
        public static int ScreenY { get; set; }
    }

    public class GridXConverter : BaseGridConverter
    {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var x = (int)value;
            return x * ScreenX / 10.0;
        }
    }

    public class GridYConverter : BaseGridConverter
    {
        public override object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var y = (int)value;
            return y * ScreenY / 6.0;
        }
    }

    public class Setup
        : MvxAndroidSetup
    {
        public Setup(Context applicationContext)
            : base(applicationContext)
        {
        }

        protected override void FillValueConverters(Cirrious.MvvmCross.Binding.Binders.IMvxValueConverterRegistry registry)
        {
            registry.AddOrOverwrite("X", new GridXConverter());
            registry.AddOrOverwrite("Y", new GridYConverter());
            registry.AddOrOverwrite("NativeColor", new MvxSimpleColorConverter());
            base.FillValueConverters(registry);
        }

        protected override IMvxApplication CreateApp()
        {
            return new App();
        }

        protected override IMvxNavigationSerializer CreateNavigationSerializer()
        {
            Cirrious.MvvmCross.Plugins.Json.PluginLoader.Instance.EnsureLoaded();
            return new MvxJsonNavigationSerializer();
        }

        protected override void InitializeLastChance()
        {
            base.InitializeLastChance();
            Cirrious.MvvmCross.Plugins.Color.PluginLoader.Instance.EnsureLoaded();
        }
    }

    [Obsolete("I'm using AbsoluteLayout - if you must use something else because of API version, then use RelativeLayout?")]
    public class BindableAbsoluteLayout : FrameLayout
    {
        public BindableAbsoluteLayout(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            var itemTemplateId = MvxListViewHelpers.ReadAttributeValue(context, attrs,
                                                                       MvxAndroidBindingResource.Instance
                                                                                              .ListViewStylableGroupId,
                                                                       MvxAndroidBindingResource.Instance
                                                                                              .ListItemTemplateId);
            Adapter = new MvxAdapterWithChangedEvent(context);
            Adapter.ItemTemplateId = itemTemplateId;
        }

        private MvxAdapterWithChangedEvent _adapter;

        public MvxAdapterWithChangedEvent Adapter
        {
            get { return _adapter; }
            set
            {
                var existing = _adapter;
                if (existing == value)
                    return;

                if (existing != null && value != null)
                {
                    existing.DataSetChanged -= AdapterOnDataSetChanged;
                    value.ItemsSource = existing.ItemsSource;
                    value.ItemTemplateId = existing.ItemTemplateId;
                }

                if (value != null)
                {
                    value.DataSetChanged += AdapterOnDataSetChanged;
                }

                if (value == null)
                {
                    MvxBindingTrace.Trace(MvxTraceLevel.Warning,
                                          "Setting Adapter to null is not recommended - you amy lose ItemsSource binding when doing this");
                }

                _adapter = value;
            }
        }

        [MvxSetToNullAfterBinding]
        public IEnumerable ItemsSource
        {
            get { return Adapter.ItemsSource; }
            set { Adapter.ItemsSource = value; }
        }

        public int ItemTemplateId
        {
            get { return Adapter.ItemTemplateId; }
            set { Adapter.ItemTemplateId = value; }
        }

        private void AdapterOnDataSetChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            this.Refill(Adapter);
        }

        public void Refill(IAdapter adapter)
        {
            RemoveAllViews();
            var count = adapter.Count;
            for (var i = 0; i < count; i++)
            {
                AddView(adapter.GetView(i, null, this));
            }
        }
    }

    [Activity(Label = "PointsOnGrid", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = ScreenOrientation.Landscape)]
    public class GridView : MvxActivityView
    {
        public new GridViewModel ViewModel
        {
            get { return (GridViewModel) base.ViewModel; }
            set { base.ViewModel = value; }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            var display = WindowManager.DefaultDisplay;
            var size = new Point();
            display.GetSize(size);
            BaseGridConverter.ScreenX = size.X;
            BaseGridConverter.ScreenY = size.Y;

            SetContentView(Resource.Layout.Main);
        }
    }
}

