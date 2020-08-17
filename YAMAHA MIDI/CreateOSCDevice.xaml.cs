using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for CreateOSCDevice.xaml
	/// </summary>
	public partial class CreateOSCDevice : Window {
		public CreateOSCDevice () {
			InitializeComponent();
		}

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

		private void textChanged (object sender, TextChangedEventArgs e) {
			checkValidInfo();
		}

		private void addButton_Click (object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		private void listenPort_PreviewTextInput (object sender, TextCompositionEventArgs e) {
			e.Handled = !IsValidPort(((TextBox)sender).Text + e.Text);
		}

		private void sendPort_PreviewTextInput (object sender, TextCompositionEventArgs e) {
			e.Handled = !IsValidPort(((TextBox)sender).Text + e.Text);
		}

		public static bool IsValidPort (string str) {
			int i;
			return int.TryParse(str, out i) && 0 < i && i <= 65535; // Make sure that the int entered is a valid UDP port
		}

		private void cancelButton_Click (object sender, RoutedEventArgs e) {
			Close();
		}
	}
}
