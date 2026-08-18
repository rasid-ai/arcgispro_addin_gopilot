using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Rasid.Services.Geometry
{
    public partial class DrawingToolbarWindow : Window
    {
        public DrawingToolbarWindow()
        {
            InitializeComponent();
        }

        private void DragSurface_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            var element = e.OriginalSource as DependencyObject;
            while (element != null && element != this)
            {
                if (element is ButtonBase)
                    return;

                element = VisualTreeHelper.GetParent(element);
            }

            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
