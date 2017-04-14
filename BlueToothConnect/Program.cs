using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;
using InTheHand.Net.Bluetooth;
using System.Threading;

namespace BlueToothConnect
{
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
                using (var cli = new BluetoothClient())
                {
                    //device.SetServiceState(service, true);
                    Console.WriteLine("Enter a PIN for the device:");

                    Pin = Console.ReadLine();


                    EventHandler<BluetoothWin32AuthenticationEventArgs> handler = new EventHandler<BluetoothWin32AuthenticationEventArgs>(HandleRequests);
                    BluetoothWin32Authentication auth = new BluetoothWin32Authentication(handler);

                    //BluetoothSecurity.RefusePinRequest(device.DeviceAddress);
                    
                    BluetoothSecurity.PairRequest(device.DeviceAddress, null);
                    //var wtf = BluetoothSecurity.SetPin(device.DeviceAddress, PIN);
                    //while (Blocker == 0) { }


                    // now it is paired
                    var service = BluetoothService.SerialPort;


                    if (!cli.Connected)
                    {
                        cli.Connect(device.DeviceAddress, service);
                    }

                    using (var stream = cli.GetStream())
                    {
                        //cli.Connect(device.DeviceAddress, service);

                        //send messages, get responses?
                        Console.WriteLine("Enter a message to send to the device:");
                        var msg = Console.ReadLine();
                        var streamPosition = 0;

                        while (msg != "x")
                        {
                            msg = msg + "\r";
                            stream.Write(msg.Select(c => (byte)c).ToArray(), 0, msg.Length);
                            streamPosition += (msg.Length - 1);
                            stream.Flush();

                            Thread.Sleep(100);

                            var builder = new StringBuilder();
                            var responseByte = stream.ReadByte();
                            var responseChar = (char)responseByte;


                            while(responseChar != '>')
                            {
                                builder.Append(responseChar);
                                responseByte = stream.ReadByte();
                                responseChar = (char)responseByte;                                
                            }

                            var responseString = builder.ToString();
                            responseString = responseString.TrimEnd('\0').Replace("\r", "\r\n");

                            

                            Console.WriteLine(String.Format("Device responded: {0}", responseString));

                            //"010C\r41 0C 0B 2B \r\r>"

                            Console.WriteLine("Enter another message, or 'x' to quit.");
                            msg = Console.ReadLine();
                        }
                    }

                    // pairing failed


                }
                BluetoothSecurity.RemoveDevice(device.DeviceAddress);

            }

            Console.WriteLine("Press any key to exit");
            var wait = Console.ReadLine();
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
