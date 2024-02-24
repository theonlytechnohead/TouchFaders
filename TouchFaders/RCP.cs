using System;
using System.Collections.Generic;
using System.Linq;

namespace TouchFaders {

    public class RCP {

        public class Message {
            public enum MessageType {
                Unknown,
                OK,
                OKm,
                NOTIFY,
                ERROR
            }
            public MessageType type;

            public string Address;
            public Address rcpAddress;

            public ErrorType errorType;
            public readonly Dictionary<ErrorType, string> errorMessages = new Dictionary<ErrorType, string>() {
            { ErrorType.None, "" },
            { ErrorType.Unknown, "Unknown error" },
            { ErrorType.UnknownCommand, "Ignored because it was an unknown command" },
            { ErrorType.WrongFormat, "Ugnored because the command parameter format was wrong and could not be interpreted" },
            { ErrorType.InvalidArgument, "Ignored because the command parameter content was outside the appropriate range and could not be interpreted" },
            { ErrorType.UnknownAddress, "Ignored because the specified address does not exist" },
            { ErrorType.UnkownEventID, "Ignored because the specified event ID does not exist" },
            { ErrorType.TooLongCommand, "Could not be interpreted because the command was too long" },
            { ErrorType.AccessDenied, "Procedure rejected because the peer device is not in an normal running state" },
            { ErrorType.Busy, "The device is busy processing, it can't recieve commands" },
            { ErrorType.ReadOnly, "Ignored because an attempt was made to set a parameter at a read-only address" },
            { ErrorType.NoPermission, "Ignored because you do not have access permission" },
            { ErrorType.InternalError, "An internal error may have occurred" }
        };

            public enum ErrorType {
                None,
                Unknown,
                UnknownCommand,
                WrongFormat,
                InvalidArgument,
                UnknownAddress,
                UnkownEventID,
                TooLongCommand,
                AccessDenied,
                Busy,
                ReadOnly,
                NoPermission,
                InternalError
            }
        }

        public static Message Parse (string message) {
            Message output = new Message {
                type = ParseType(message.Split(' ').First())
            };
            output.Address = message.Split(' ')[1];
            switch (output.type) {
                case Message.MessageType.Unknown:
                    break;
                case Message.MessageType.OK:
                    break;
                case Message.MessageType.OKm:
                    break;
                case Message.MessageType.NOTIFY:
                    break;
                case Message.MessageType.ERROR:
                    output.errorType = ParseError(message.Split(' ').Last());
                    break;
            }
            return output;
        }

        private static Message.MessageType ParseType (string header) {
            return header switch {
                "OK" => Message.MessageType.OK,
                "OKm" => Message.MessageType.OKm,
                "NOTIFY" => Message.MessageType.NOTIFY,
                "ERROR" => Message.MessageType.ERROR,
                _ => Message.MessageType.Unknown,
            };
        }

        private static Message.ErrorType ParseError (string errorCode) {
            return errorCode switch {
                "UnknownCommand" => Message.ErrorType.UnknownCommand,
                "WrongFormat" => Message.ErrorType.WrongFormat,
                "InvalidArgument" => Message.ErrorType.InvalidArgument,
                "UnknownAddress" => Message.ErrorType.UnknownAddress,
                "UnkownEventID" => Message.ErrorType.UnkownEventID,
                "TooLongCommand" => Message.ErrorType.TooLongCommand,
                "AccessDenied" => Message.ErrorType.AccessDenied,
                "Busy" => Message.ErrorType.Busy,
                "ReadOnly" => Message.ErrorType.ReadOnly,
                "NoPermission" => Message.ErrorType.NoPermission,
                "InternalError" => Message.ErrorType.InternalError,
                _ => Message.ErrorType.Unknown,
            };
        }

    }

    public class Address {
        // parameters: X, Y, min, max, default, unit, type, UI?, r/w, scale
        // get: X, Y
        // set: X, Y, value, textValue

        public static object Parse (string address, string parameters) {
            Type currentType = typeof(Console);
            var components = address.Split(':');

            while (address != string.Empty) {
                bool found = false;
                string current = components[0];
                if (2 <= components.Length) {
                    address = components[1];
                } else {
                    address = "";
                }
                foreach (var type in currentType.GetNestedTypes()) {
                    if (type.Name == current) {
                        currentType = type;
                        found = true;
                        components = address.Split(new[] { '/' }, 2);
                    }
                }
                if (!found) {
                    currentType = null;
                    break;
                }
            }

            if (currentType != null) {
                var parseMethod = currentType.GetMethod("Parse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var instance = parseMethod.Invoke(null, new[] { parameters });
                return instance;
            } else {
                return new object();
            }
        }

        public static string ToString (object instance) {
            Type currentType = instance.GetType();
            string result = string.Empty;

            while (currentType != null && currentType != typeof(object)) {
                if (currentType.DeclaringType == typeof(Console)) {
                    result = $"{currentType.Name}:{result}";
                    currentType = null;
                } else {
                    result = $"{currentType.Name}/{result}";
                    currentType = currentType.DeclaringType;
                }
            }

            return result.TrimEnd('/') + " " + instance;
        }

        public class Console {
            public class MIXER {
                public class Current {
                    public class InCh {
                        public class Fader {
                            public class Level {
                                // 72 1 -32768 1000 -32768 "dB" integer any rw 100
                                public int channel;
                                public int? level = null;
                                public Level (int channel) {
                                    this.channel = channel;
                                }
                                public Level (int channel, int level) : this(channel) {
                                    this.level = level;
                                }
                                public static Level Parse (string parameters) {
                                    var components = parameters.Split(' ');
                                    var channel = int.Parse(components[0]);
                                    var y = int.Parse(components[1]);
                                    if (components.Length == 2) {
                                        return new Level(channel, y);
                                    }
                                    var level = int.Parse(components[2]);
                                    return new Level(channel, level);
                                }
                                public override string ToString () {
                                    return channel + " 0" + (level != null ? " " + level : string.Empty);
                                }
                            }
                            public class On {
                                // 72 1 0 1 1 "" integer any rw 1
                                public int channel;
                                public int? on = null;
                                public On (int channel) {
                                    this.channel = channel;
                                }
                                public On (int channel, int on) : this(channel) {
                                    this.on = on;
                                }
                                public override string ToString () {
                                    return channel + " 0" + (on != null ? " " + on : string.Empty);
                                }
                            }
                        }
                        public class ToMix {
                            public class Level {
                                // 72 24 -32768 1000 -32768 "dB" integer any rw 100
                                public int channel;
                                public int mix;
                                public int? level = null;
                                public Level (int channel, int mix) {
                                    this.channel = channel;
                                    this.mix = mix;
                                }
                                public Level (int channel, int mix, int level) : this(channel, mix) {
                                    this.level = level;
                                }
                                public override string ToString () {
                                    return channel + " " + mix + (level != null ? " " + level : string.Empty);
                                }
                            }
                            public class On {
                                // 72 24 0 1 1 "" integer any rw 1
                                public int channel;
                                public int mix;
                                public int? on = null;
                                public On (int channel, int mix) {
                                    this.channel = channel;
                                    this.mix = mix;
                                }
                                public On (int channel, int mix, int on) : this(channel, mix) {
                                    this.on = on;
                                }
                                public override string ToString () {
                                    return channel + " " + mix + (on != null ? " " + on : string.Empty);
                                }
                            }
                        }
                    }
                }
            }

            public class CL {
                // TODO
            }

            public class QL {
                public class Current {
                    public class CustomFaderBank {
                        public class SourceCh {
                            // 4 32 0 11 "NO ASSIGN" "" string any rw 1
                        }
                        public class Master {
                            public class SourceCh {
                                // 4 2 0 11 "NO ASSIGN" "" string any rw 1
                            }
                        }
                    }
                    public class FaderBank {
                        public enum FaderBanks {
                            I,
                            Dont,
                            Know
                        }
                        public class Select {
                            // 1 1 0 1 0 "" integer any rw 1
                            public enum Selects {
                                BankA,
                                BankB
                            }
                            private readonly Selects? bank = null;
                            public Select () { }
                            public Select (Selects bank) { this.bank = bank; }
                            public override string ToString () {
                                return "0 0" + bank != null ? " " + bank.Value.ToString() : string.Empty;
                            }
                        }
                        public class Bank {
                            public enum Banks {
                                Input1,
                                Input2,
                                StInDCA,
                                MixMatrix,
                                B1,
                                B2,
                                B3,
                                B4
                            }
                            public class Recall {
                                // 1 3 0 8 0 "" integer any rw 1
                                public FaderBanks? faderBank = null;
                                public Banks? bank = null;
                                public Recall (FaderBanks faderBank) {
                                    this.faderBank = faderBank;
                                }
                                public Recall (FaderBanks faderBank, Banks bank) : this(faderBank) {
                                    this.bank = bank;
                                }
                                public override string ToString () {
                                    return "0 " + (int)faderBank.Value + (bank != null ? ' ' + ((int)bank.Value).ToString() : string.Empty);
                                }
                            }
                            public class Toggle {
                                // 1 1 0 8 0 "" integer any rw 1
                            }
                        }
                    }
                }
            }
        }
    }
}
