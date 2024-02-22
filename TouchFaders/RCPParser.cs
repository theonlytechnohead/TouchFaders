using System.Collections.Generic;
using System.Linq;

namespace TouchFaders {

    public class RCPMessage {
        public enum MessageType {
            Unknown,
            OK,
            OKm,
            NOTIFY,
            ERROR
        }
        public MessageType type;

        public string Address;

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

    internal class RCPParser {

        public static RCPMessage Parse (string message) {
            RCPMessage output = new RCPMessage {
                type = ParseType(message.Split(' ').First())
            };
            output.Address = message.Split(' ')[1];
            switch (output.type) {
                case RCPMessage.MessageType.Unknown:
                    break;
                case RCPMessage.MessageType.OK:
                    break;
                case RCPMessage.MessageType.OKm:
                    break;
                case RCPMessage.MessageType.NOTIFY:
                    break;
                case RCPMessage.MessageType.ERROR:
                    output.errorType = ParseError(message.Split(' ').Last());
                    break;
            }
            return output;
        }

        private static RCPMessage.MessageType ParseType (string header) {
            return header switch {
                "OK" => RCPMessage.MessageType.OK,
                "OKm" => RCPMessage.MessageType.OKm,
                "NOTIFY" => RCPMessage.MessageType.NOTIFY,
                "ERROR" => RCPMessage.MessageType.ERROR,
                _ => RCPMessage.MessageType.Unknown,
            };
        }

        private static RCPMessage.ErrorType ParseError (string errorCode) {
            return errorCode switch {
                "UnknownCommand" => RCPMessage.ErrorType.UnknownCommand,
                "WrongFormat" => RCPMessage.ErrorType.WrongFormat,
                "InvalidArgument" => RCPMessage.ErrorType.InvalidArgument,
                "UnknownAddress" => RCPMessage.ErrorType.UnknownAddress,
                "UnkownEventID" => RCPMessage.ErrorType.UnkownEventID,
                "TooLongCommand" => RCPMessage.ErrorType.TooLongCommand,
                "AccessDenied" => RCPMessage.ErrorType.AccessDenied,
                "Busy" => RCPMessage.ErrorType.Busy,
                "ReadOnly" => RCPMessage.ErrorType.ReadOnly,
                "NoPermission" => RCPMessage.ErrorType.NoPermission,
                "InternalError" => RCPMessage.ErrorType.InternalError,
                _ => RCPMessage.ErrorType.Unknown,
            };
        }

    }
}
