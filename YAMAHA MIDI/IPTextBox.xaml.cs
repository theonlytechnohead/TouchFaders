﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YAMAHA_MIDI {
	/// <summary>
	/// Interaction logic for IPTextBox.xaml
	/// </summary>
	public partial class IPTextBox : UserControl {
		private static readonly List<Key> DigitKeys = new List<Key> { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.NumPad0, Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4, Key.NumPad5, Key.NumPad6, Key.NumPad7, Key.NumPad8, Key.NumPad9 };
		private static readonly List<Key> MoveForwardKeys = new List<Key> { Key.Right };
		private static readonly List<Key> MoveBackwardKeys = new List<Key> { Key.Left };
		private static readonly List<Key> OtherAllowedKeys = new List<Key> { Key.Tab, Key.Delete };

		private readonly List<TextBox> _segments = new List<TextBox>();

		private bool _suppressAddressUpdate = false;

		public TextChangedEventHandler AddressChangedEvent;

		public IPTextBox () {
			InitializeComponent();
			_segments.Add(FirstSegment);
			_segments.Add(SecondSegment);
			_segments.Add(ThirdSegment);
			_segments.Add(LastSegment);
		}

		public static readonly DependencyProperty AddressProperty = DependencyProperty.Register(
			"Address", typeof(string), typeof(IPTextBox), new FrameworkPropertyMetadata(default(string), AddressChanged) {
				BindsTwoWayByDefault = true
			});

		private static void AddressChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e) {
			var ipTextBox = dependencyObject as IPTextBox;
			var text = e.NewValue as string;

			if (text != null && ipTextBox != null) {
				ipTextBox._suppressAddressUpdate = true;
				var i = 0;
				foreach (var segment in text.Split('.')) {
					ipTextBox._segments[i].Text = segment;
					i++;
				}
				ipTextBox._suppressAddressUpdate = false;
			}
		}

		public string Address {
			get { return (string)GetValue(AddressProperty); }
			set { SetValue(AddressProperty, value); }
		}

		private void UIElement_OnPreviewKeyDown (object sender, KeyEventArgs e) {
			if (DigitKeys.Contains(e.Key)) {
				e.Handled = ShouldCancelDigitKeyPress(e.Key);
				HandleDigitPress();
			} else if (MoveBackwardKeys.Contains(e.Key)) {
				e.Handled = ShouldCancelBackwardKeyPress();
				HandleBackwardKeyPress();
			} else if (MoveForwardKeys.Contains(e.Key)) {
				e.Handled = ShouldCancelForwardKeyPress();
				HandleForwardKeyPress();
			} else if (e.Key == Key.Back) {
				HandleBackspaceKeyPress();
			} else if (e.Key == Key.OemPeriod || e.Key == Key.Decimal) {
				e.Handled = true;
				HandlePeriodKeyPress();
			} else {
				e.Handled = !AreOtherAllowedKeysPressed(e);
			}
		}

		private bool AreOtherAllowedKeysPressed (KeyEventArgs e) {
			return e.Key == Key.C && ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) ||
				   e.Key == Key.V && ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) ||
				   e.Key == Key.A && ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) ||
				   e.Key == Key.X && ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0) ||
				   OtherAllowedKeys.Contains(e.Key);
		}

		private void HandleDigitPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.Text.Length == 3 &&
				currentTextBox.CaretIndex == 3 && currentTextBox.SelectedText.Length == 0) {
				MoveFocusToNextSegment(currentTextBox);
			}
		}

		private bool ShouldCancelDigitKeyPress (Key key) {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;
			if (currentTextBox.Text.Length == 2) {
				int keyVal = (int)key;
				int value = 0;
				if (keyVal >= (int)Key.D0 && keyVal <= (int)Key.D9) { // Normal keys
					value = keyVal - (int)Key.D0;
				} else if (keyVal >= (int)Key.NumPad0 && keyVal <= (int)Key.NumPad9) { // Number pad keys
					value = keyVal - (int)Key.NumPad0;
				}
				value = int.Parse(currentTextBox.Text + value.ToString());
				if (255 <= value)
					return true;
			}
			return currentTextBox != null &&
				   currentTextBox.Text.Length == 3 &&
				   currentTextBox.CaretIndex == 3 &&
				   currentTextBox.SelectedText.Length == 0;
		}

		private void TextBoxBase_OnTextChanged (object sender, TextChangedEventArgs e) {
			TextChangedEventHandler handler = AddressChangedEvent;
			handler?.Invoke(this, e);
			if (!_suppressAddressUpdate) {
				Address = string.Format("{0}.{1}.{2}.{3}", FirstSegment.Text, SecondSegment.Text, ThirdSegment.Text, LastSegment.Text);
			}

			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.Text.Length == 3 && currentTextBox.CaretIndex == 3) {
				MoveFocusToNextSegment(currentTextBox);
			}
		}

		public bool hasValidAddress () {
			if (FirstSegment.Text != "" && SecondSegment.Text != "" && ThirdSegment.Text != "" && LastSegment.Text != "") {
				return true;
			} else {
				return false;
			}
		}

		private bool ShouldCancelBackwardKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;
			return currentTextBox != null && currentTextBox.CaretIndex == 0;
		}

		private void HandleBackspaceKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.CaretIndex == 0 && currentTextBox.SelectedText.Length == 0) {
				MoveFocusToPreviousSegment(currentTextBox);
			}
		}

		private void HandleBackwardKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.CaretIndex == 0) {
				MoveFocusToPreviousSegment(currentTextBox);
			}
		}

		private bool ShouldCancelForwardKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;
			return currentTextBox != null && currentTextBox.CaretIndex == 3;
		}

		private void HandleForwardKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.CaretIndex == currentTextBox.Text.Length) {
				MoveFocusToNextSegment(currentTextBox);
			}
		}

		private void HandlePeriodKeyPress () {
			var currentTextBox = FocusManager.GetFocusedElement(this) as TextBox;

			if (currentTextBox != null && currentTextBox.Text.Length > 0 && currentTextBox.CaretIndex == currentTextBox.Text.Length) {
				MoveFocusToNextSegment(currentTextBox);
			}
		}

		private void MoveFocusToPreviousSegment (TextBox currentTextBox) {
			if (!ReferenceEquals(currentTextBox, FirstSegment)) {
				var previousSegmentIndex = _segments.FindIndex(box => ReferenceEquals(box, currentTextBox)) - 1;
				currentTextBox.SelectionLength = 0;
				currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
				_segments[previousSegmentIndex].CaretIndex = _segments[previousSegmentIndex].Text.Length;
			}
		}

		private void MoveFocusToNextSegment (TextBox currentTextBox) {
			if (!ReferenceEquals(currentTextBox, LastSegment)) {
				currentTextBox.SelectionLength = 0;
				currentTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
			}
		}

		private void DataObject_OnPasting (object sender, DataObjectPastingEventArgs e) {
			var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
			if (!isText) {
				e.CancelCommand();
				return;
			}

			var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;

			int num;

			if (!int.TryParse(text, out num)) {
				e.CancelCommand();
			}
		}
	}
}
