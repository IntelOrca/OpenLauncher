using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace openlauncher
{
    public partial class AlertBox : UserControl
    {
        private bool _loaded;

        public AlertBox()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            if (Design.IsDesignMode)
            {
                Title = "Failed to obtain builds";
                Message = "API rate limit exceeded for 0.0.0.0. (But here's the good news: Authenticated requests get a higher rate limit. Check out the documentation for more details.)";
            }
            _loaded = true;
            OnKindUpdate();
        }

        private void OnKindUpdate()
        {
            if (!_loaded)
                return;

            var iconControl = this.FindControl<PathIcon>("icon");
            switch (Kind)
            {
                case AlertKind.Error:
                {
                    Background = new SolidColorBrush(0xFFFFCCCC);
                    if (Resources.TryGetResource("error_circle_regular", out var resource))
                    {
                        iconControl.Data = resource as Geometry;
                    }
                    break;
                }
                case AlertKind.Warning:
                {
                    Background = new SolidColorBrush(0xFFFFFFCC);
                    if (Resources.TryGetResource("warning_regular", out var resource))
                    {
                        iconControl.Data = resource as Geometry;
                    }
                    break;
                }
            }
        }

        public static readonly StyledProperty<AlertKind> KindProperty =
            AvaloniaProperty.Register<AlertBox, AlertKind>(nameof(Kind), notifying: (o, e) =>
            {
                if (!e)
                    (o as AlertBox)?.OnKindUpdate();
            });

        public AlertKind Kind
        {
            get { return GetValue(KindProperty); }
            set { SetValue(KindProperty, value); }
        }

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<AlertBox, string>(nameof(Title));

        public string Title
        {
            get { return GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly StyledProperty<object> MessageProperty =
            AvaloniaProperty.Register<AlertBox, object>(nameof(Message));

        public object Message
        {
            get { return GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }
    }

    public enum AlertKind
    {
        Error,
        Warning
    }
}
