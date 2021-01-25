using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TouchFaders_MIDI {
	/// <summary>
	/// Interaction logic for CreateOSCDevice.xaml
	/// </summary>
	public partial class CreateOSCDevice : Window {
		public CreateOSCDevice () {
			InitializeComponent();
		}

		#region Scaling
		// This section smoothly scales everything within the mainGrid
		public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
			typeof(double),
			typeof(CreateOSCDevice),
			new UIPropertyMetadata(1.0,
				new PropertyChangedCallback(OnScaleValueChanged),
				new CoerceValueCallback(OnCoerceScaleValue)));

		private static object OnCoerceScaleValue (DependencyObject o, object value) {
			CreateOSCDevice createOSCWindow = o as CreateOSCDevice;
			if (createOSCWindow != null)
				return createOSCWindow.OnCoerceScaleValue((double)value);
			else
				return value;
		}

		private static void OnScaleValueChanged (DependencyObject o, DependencyPropertyChangedEventArgs e) {
			CreateOSCDevice createOSCWindow = o as CreateOSCDevice;
			if (createOSCWindow != null)
				createOSCWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
		}

		protected virtual double OnCoerceScaleValue (double value) {
			if (double.IsNaN(value))
				return 1.0f;

			value = Math.Max(1f, value);
			return value;
		}

		protected virtual void OnScaleValueChanged (double oldValue, double newValue) {
			// Don't need to do anything
		}

		public double ScaleValue {
			get {
				return (double)GetValue(ScaleValueProperty);
			}
			set {
				SetValue(ScaleValueProperty, value);
			}
		}

		private void createOSCGrid_SizeChanged (object sender, SizeChangedEventArgs e) {
			CalculateScale();
		}

		private void CalculateScale () {
			double xScale = ActualWidth / 300f; // must be set to initial window sizing for proper scaling!!!
			double yScale = ActualHeight / 200f; // must be set to initial window sizing for proper scaling!!!
			double value = Math.Min(xScale, yScale); // Ensure that the smallest axis is the one that controls the scale
			ScaleValue = (double)OnCoerceScaleValue(createOSCWindow, value); // Update the actual scale for the main window
		}

		#endregion

		private void Window_Loaded (object sender, RoutedEventArgs e) {
			name.Focus();
			//addressIPTextBox.FirstSegment.Focus();
			addressIPTextBox.AddressChangedEvent = textChanged;
		}

		private void checkValidInfo () {
			if (addressIPTextBox.hasValidAddress() && sendPort.Text.Length != 0 && listenPort.Text.Length != 0) {
				addButton.IsEnabled = true;
			} else {
				addButton.IsEnabled = false;
			}
		}

		private void textChanged (object sender, TextChangedEventArgs e) => checkValidInfo();

		private void addButton_Click (object sender, RoutedEventArgs e) => DialogResult = true;

		private void listenPort_PreviewTextInput (object sender, TextCompositionEventArgs e) => e.Handled = !IsValidPort(((TextBox)sender).Text + e.Text);

		private void sendPort_PreviewTextInput (object sender, TextCompositionEventArgs e) => e.Handled = !IsValidPort(((TextBox)sender).Text + e.Text);

		public static bool IsValidPort (string str) {
			return int.TryParse(str, out int i) && 0 < i && i <= 65535; // Make sure that the int entered is a valid UDP port
		}

		private void cancelButton_Click (object sender, RoutedEventArgs e) => Close();
	}
}
