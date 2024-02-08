namespace TouchFaders {
    interface IConsole {
        void Connect (string address);
        void Sync ();
        void Send (string message);
        void Disconnect ();
    }
}
