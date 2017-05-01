using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using System.Threading;
using System.IO;

namespace BlueToothConnect
{
    enum RunModes
    {
        SingleResponse = 1,
        DataLogging = 2,
        Exit = 3
    }

    class Program
    {
        static readonly Guid MyServiceUuid
      = new Guid("{00112233-4455-6677-8899-aabbccddeeff}");

        private static string Pin { get; set; }
        private static int Blocker { get; set; }
        static void Main(string[] args)
        {
            Console.WriteLine("Starting up...");

            var device = SearchForDevices();

            if (device != null)
            {

                //connect to device
                using (var cli = SetUpDeviceConnection(device))
                {

                    //What do we want to do?
                    var runMode = SelectRunMode();

                    using (var stream = cli.GetStream())
                    {
                        switch (runMode)
                        {
                            case RunModes.SingleResponse:
                                RunInSingleResponseMode(stream).GetAwaiter().GetResult();
                                break;
                            case RunModes.DataLogging:
                                RunInDataLoggingMode(stream).GetAwaiter().GetResult();
                                break;
                        }

                    }

                }
                BluetoothSecurity.RemoveDevice(device.DeviceAddress);

            }

            Console.WriteLine("Press any key to exit");
            var wait = Console.ReadLine();
        }

        private static BluetoothClient SetUpDeviceConnection(BluetoothDeviceInfo device)
        {
            var cli = new BluetoothClient();

            Console.WriteLine("Enter a PIN for the device:");

            Pin = Console.ReadLine();

            var handler = new EventHandler<BluetoothWin32AuthenticationEventArgs>(HandleRequests);
            var auth = new BluetoothWin32Authentication(handler);

            BluetoothSecurity.PairRequest(device.DeviceAddress, null);

            // now it is paired
            var service = BluetoothService.SerialPort;

            if (!cli.Connected)
            {
                cli.Connect(device.DeviceAddress, service);
            }

            return cli;
        }

        private static RunModes SelectRunMode()
        {
            Console.WriteLine("Please select a run mode:");
            Console.WriteLine("1 - Single Response");
            Console.WriteLine("2 - Data Logging");
            Console.WriteLine("3 - Exit");

            var selection = ReadLineInt();

            //there are more elegant ways to do this, but this way is foolproof
            switch (selection)
            {
                case 1:
                    return RunModes.SingleResponse;
                case 2:
                    return RunModes.DataLogging;
                default:
                    return RunModes.Exit;
            }

        }

        private static async Task RunInDataLoggingMode(System.Net.Sockets.NetworkStream stream)
        {

            Console.WriteLine("Enter the number of data points to capture:");
            var pointsToCapture = ReadLineInt();
            var responses = new Queue<string>(pointsToCapture);

            Console.WriteLine("Enter the name of the log file: ");
            var logFileName = Console.ReadLine();

            //send messages, get responses?
            //"010C\r41 0C 0B 2B \r\r>"
            Console.WriteLine("Enter messages, comma separated, to send to the device:");
            var msg = Console.ReadLine();
            
            if (msg == "x")
                return;

            var msgGroup = msg.Split(',');

            while (responses.Count < pointsToCapture * msgGroup.Count())
            {
                foreach (var singleMsg in msgGroup)
                {
                    var responseString = await SendCommand(singleMsg, stream);

                    responses.Enqueue(responseString);

                    Console.Write("Logged value {2}: {0}{1}", responseString, Environment.NewLine, responses.Count);
                    //Thread.Sleep(10);
                }
            }

            await WriteLogFile(responses, logFileName);

        }

        private static async Task WriteLogFile(Queue<string> responses, string logFileName)
        {
            using (var filestream = File.Open(logFileName, FileMode.OpenOrCreate))
            {
                while (responses.Any())
                {
                    var response = responses.Dequeue();
                    
                    var responseBytes = Encoding.ASCII.GetBytes(response);

                    await filestream.WriteAsync(responseBytes, 0, responseBytes.Count());                    
                }
            }

            Console.WriteLine("Log file saved to:" + Path.GetFullPath(logFileName));
        }

        private static int ReadLineInt()
        {
            var result = Console.ReadLine().Trim();

            return Int32.Parse(result); //Ok to throw an error, fail loudly
        }

        private static async Task RunInSingleResponseMode(System.Net.Sockets.NetworkStream stream)
        {
            //cli.Connect(device.DeviceAddress, service);

            //send messages, get responses?
            Console.WriteLine("Enter a message to send to the device:");
            var msg = Console.ReadLine();

            while (msg != "x")
            {
                var responseString = await SendCommand(msg, stream);

                Console.WriteLine(String.Format("Device responded: {0}", responseString));

                //"010C\r41 0C 0B 2B \r\r>"

                Console.WriteLine("Enter another message, or 'x' to quit.");
                msg = Console.ReadLine();
            }
        }

        private static async Task<string> SendCommand(string message, System.Net.Sockets.NetworkStream stream)
        {
            var command = message + "\r";
            await stream.WriteAsync(command.Select(c => (byte)c).ToArray(), 0, command.Length);

            await stream.FlushAsync();

            //maybe don't need to sleep async? let's try it...
            //Thread.Sleep(100);

            var builder = new StringBuilder();
            var buffer = new byte[1];
            await stream.ReadAsync(buffer, 0, 1);
            var responseChar = (char)buffer[0];

            while (responseChar != '>')
            {
                builder.Append(responseChar);
                await stream.ReadAsync(buffer, 0, 1);
                responseChar = (char)buffer[0];
            }

            var responseString = builder.ToString();
            responseString = responseString.TrimEnd('\0').Replace("\r", "\r\n");

            return responseString;
        }

        private static void HandleRequests(object that, BluetoothWin32AuthenticationEventArgs e)
        {
            if (e.AuthenticationMethod == BluetoothAuthenticationMethod.Legacy)
            {

                e.Pin = Pin;
                //BluetoothSecurity.SetPin(e.Device.DeviceAddress, Pin);
            }
            e.Confirm = true;
            Blocker++;
        }

        private static BluetoothDeviceInfo SearchForDevices()
        {
            string blocker = string.Empty;

            while (blocker != "x")
            {
                var cli = new BluetoothClient();

                var peers = cli.DiscoverDevicesInRange();

                for (int i = 1; i <= peers.Count(); i++)
                {
                    Console.WriteLine(String.Format("{0} - {1}", i, peers[i - 1].DeviceName));
                }

                Console.WriteLine(String.Format("Press 'x' to end searching, a number 1 - {0} to select a device, any other key to search again.", peers.Count()));

                blocker = Console.ReadLine();

                int selection = 0;

                if (Int32.TryParse(blocker, out selection))
                {
                    return peers[selection - 1];
                }
            }

            return null;

        }
    }
}
