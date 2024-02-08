namespace TouchFaders {
    public class AppConfiguration {

        public const string CONFIG_DIR = "config";
        public const string CONFIG_FILE = "config";
        public const string DATA_FILE = "data";

        // Constants and stuff goes here
        public class Config {
            public Mixer MIXER { get; set; }
            public int NUM_CHANNELS { get; set; }
            public int NUM_MIXES { get; set; }

            public static Config defaultValues () {
                return new Config() {
                    MIXER = Mixer.QL1,
                    NUM_MIXES = 16,
                    NUM_CHANNELS = 32
                };
            }
        }
    }

}
