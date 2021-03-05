using System;
using System.Windows.Threading;

namespace TouchFaders_MIDI {
	class MIDI_Functions {

		/// <summary>
		/// Called before OnCreated
		/// </summary>
		public EventHandler OnCreate;
		/// <summary>
		/// Called before OnStart
		/// </summary>
		public EventHandler OnStart;
		/// <summary>
		/// Called before OnStop
		/// </summary>
		public EventHandler OnStop;
		/// <summary>
		/// Called before OnDestroyed
		/// </summary>
		public EventHandler OnDestroy;

		/// <summary>
		/// Checks if MIDI is ready
		/// <para>
		/// <c>true</c> if MIDI is running and sync'd, <c>false</c> otherwise
		/// </para>
		/// </summary>
		public bool IsMIDIRunning {
			get {
				bool running = false;
				Dispatcher.CurrentDispatcher.Invoke(() => {
					running = MainWindow.instance.midiProgressBar.Value >= MainWindow.instance.midiProgressBar.Maximum;
				});
				return running;
			}
		}

		MIDI_Functions () {
			OnCreated();
		}

		~MIDI_Functions () {
			OnDestroyed();
		}

		public void OnCreated () {
			OnCreate?.Invoke(this, new EventArgs());
		}

		public void OnStarted () {
			OnStart?.Invoke(this, new EventArgs());
		}

		public void OnEventReceived () {

		}

		public void OnStopped () {
			OnStop?.Invoke(this, new EventArgs());
		}

		public void OnDestroyed () {
			OnDestroy?.Invoke(this, new EventArgs());
		}

	}
}
