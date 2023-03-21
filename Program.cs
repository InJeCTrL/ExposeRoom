using System.CommandLine;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PacketDotNet;
using SharpPcap;

namespace ExposeRoom;

class Program
{
    static readonly CaptureDeviceList devices;
    static readonly Dictionary<string, List<IPAddress>> adapterIP;

    static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("Help to expose & find game room on internal network.");

        var durationArg = new Argument<int>(
            "duration",
            description: "Running duration in minutes",
            getDefaultValue: () => 5);
        rootCommand.AddArgument(durationArg);

        rootCommand.SetHandler((durationArg) =>
        {
            Runner(durationArg);
        }, durationArg);

        await rootCommand.InvokeAsync(args);
    }

    static Program()
    {
        devices = CaptureDeviceList.Instance;
        if (devices.Count == 0)
        {
            Console.WriteLine("No network interfaces found");
            Environment.Exit(0);
        }

        adapterIP = new();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            var adapterName = adapter.Name;
            if (!adapterIP.ContainsKey(adapterName))
            {
                adapterIP.Add(adapterName, new());
            }

            foreach (var addr in adapter.GetIPProperties().UnicastAddresses)
            {
                adapterIP[adapterName].Add(addr.Address);
            }
        }
    }

    static void Runner(int duration)
    {
        devices[0].OnPacketArrival += new PacketArrivalEventHandler(DeviceOnPackArrival);
        devices[0].Open(DeviceModes.Promiscuous);
        devices[0].Filter = "udp";

        devices[0].StartCapture();
        Thread.Sleep(duration * 60 * 1000);
        devices[0].StopCapture();
        devices[0].Close();
    }

    static bool IsMultiCastAddr(IPAddress addr)
    {
        if (addr.IsIPv6Multicast)
        {
            return true;
        }

        if (addr.AddressFamily.Equals(AddressFamily.InterNetwork) &&
            addr.GetAddressBytes()[0] >= 224)
        {
            return true;
        }

        return false;
    }

    static void DeviceOnPackArrival(object sender, PacketCapture e)
    {
        var oriEthPacket = (EthernetPacket)EthernetPacket.ParsePacket(e.GetPacket().LinkLayerType, e.Data.ToArray());
        if (oriEthPacket.PayloadPacket is IPPacket oriIpPacket &&
            IsMultiCastAddr(oriIpPacket.DestinationAddress))
        {
            var oriUdpPacket = oriIpPacket.Extract<UdpPacket>();
            var destIpAddr = oriIpPacket.DestinationAddress;

            for (int idx = 1; idx < devices.Count; ++idx)
            {
                var adapterName = devices[idx].Name;
                if (!adapterIP.ContainsKey(adapterName))
                {
                    continue;
                }

                if (devices[idx].MacAddress == null)
                {
                    continue;
                }

                try
                {
                    devices[idx].Open();
                    var ethPacket = new EthernetPacket(devices[idx].MacAddress, PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF"), oriEthPacket.Type);

                    foreach (var srcIpAddr in adapterIP[adapterName])
                    {
                        oriIpPacket.SourceAddress = srcIpAddr;
                        oriIpPacket.DestinationAddress = destIpAddr;
                        ethPacket.PayloadData = oriIpPacket.Bytes;
                        devices[idx].SendPacket(ethPacket);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    devices[idx].Close();
                }
            }
        }
    }
}