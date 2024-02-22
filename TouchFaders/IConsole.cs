namespace TouchFaders {
    interface IConsole {
        void Connect (string address, System.Action started, System.Action<string> startFailed);
        void Sync ();
        void Send (string message);
        void Disconnect ();
    }
}
