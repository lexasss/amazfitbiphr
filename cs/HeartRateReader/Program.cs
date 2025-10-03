using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace HeartRateReader
{
    class Program
    {
        // Replace with your MAC address (no colons, uppercase hex)
        const string MAC_ADDRESS = "DA8190CBF322";

        // Heart Rate UUIDs
        //static readonly Guid HEART_RATE_SERVICE_UUID = Guid.Parse("0000180D-0000-1000-8000-00805f9b34fb");
        //static readonly Guid HEART_RATE_MEASUREMENT_UUID = Guid.Parse("00002A37-0000-1000-8000-00805f9b34fb");

        static async Task Main(string[] args)
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                Console.WriteLine("Bluetooth is not available.");
                return;
            }
            else if (!adapter.IsLowEnergySupported)
            {
                Console.WriteLine("Bluetooth LE is not available.");
                return;
            }
            else 
            {
                Console.WriteLine($"Found bluetooth adapter ({adapter.DeviceId})");
            }

            ulong btAddress = Convert.ToUInt64(MAC_ADDRESS, 16);
            var device = await BluetoothLEDevice.FromBluetoothAddressAsync(btAddress);

            if (device == null)
            {
                Console.WriteLine("Device not found.");
                return;
            }

            Console.WriteLine($"Connected to: {device.Name}");

            var result = await device.GetGattServicesAsync();

            if (result.Status != GattCommunicationStatus.Success)
            {
                Console.WriteLine("Could not get GATT services.");
                return;
            }

            var hrService = result.Services.FirstOrDefault(s => s.Uuid == GattServiceUuids.HeartRate);
            if (hrService == null)
            {
                Console.WriteLine("Heart Rate service not found.");
                return;
            }

            var characteristicsResult = await hrService.GetCharacteristicsAsync();
            var hrCharacteristic = characteristicsResult.Characteristics
                .FirstOrDefault(c => c.Uuid == GattCharacteristicUuids.HeartRateMeasurement);

            if (hrCharacteristic == null)
            {
                Console.WriteLine("Heart Rate Measurement characteristic not found.");
                return;
            }

            Console.WriteLine("Subscribing to heart rate notifications...");

            hrCharacteristic.ValueChanged += (sender, args) =>
            {
                var data = args.CharacteristicValue.ToArray();

                if (data.Length > 1)
                {
                    int flags = data[0];
                    int hrValue = (flags & 0x01) == 0 ? data[1] : (data[1] | (data[2] << 8));
                    Console.WriteLine($"Heart Rate: {hrValue} bpm");
                }
            };

            var status = await hrCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (status != GattCommunicationStatus.Success)
            {
                Console.WriteLine("Failed to subscribe to notifications.");
                return;
            }

            Console.WriteLine("Listening... Press any key to exit.");
            Console.ReadKey();

            // Cleanup
            await hrCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.None);
        }
    }
}

/*
internal class Program
{
    private static async Task ListenDevice(BluetoothDevice device)
    {
        Console.WriteLine("Connecting...");
        await device.ConnectAsync();

        Console.WriteLine("Discovering services...");
        var services = await device.GetGattServicesAsync();

        // Heart Rate service UUID is 0x180D
        Guid heartRateServiceUuid = GattServiceUuids.HeartRate;  // or new Guid("0000180D-0000-1000-8000-00805f9b34fb");
        GattDeviceService hrService = null;

        foreach (var s in services)
        {
            if (s.Uuid == heartRateServiceUuid)
            {
                hrService = s;
                break;
            }
        }

        if (hrService == null)
        {
            Console.WriteLine("Heart rate service not found.");
            return;
        }

        Console.WriteLine("Getting characteristics...");
        var characteristics = await hrService.GetCharacteristicsAsync();
        GattCharacteristic hrChar = null;

        foreach (var c in characteristics)
        {
            // Heart rate measurement char UUID 0x2A37
            if (c.Uuid == GattCharacteristicUuids.HeartRateMeasurement)  // or new Guid("00002A37-0000-1000-8000-00805f9b34fb")
            {
                hrChar = c;
                break;
            }
        }

        if (hrChar == null)
        {
            Console.WriteLine("Heart rate measurement characteristic not found.");
            return;
        }

        Console.WriteLine("Subscribing to heart rate notifications...");
        hrChar.ValueChanged += (s, args2) =>
        {
            byte[] data = args2.Value.ToArray();
            // Parse according to BLE Heart Rate spec:
            // First byte: flags; bit0 = 0 => uint8 heart rate, bit0=1 => uint16 heart rate
            bool hr16 = (data[0] & 0x01) != 0;
            int hrValue;
            if (!hr16)
            {
                hrValue = data[1];
            }
            else
            {
                hrValue = BitConverter.ToUInt16(data, 1);
            }
            Console.WriteLine($"Heart rate: {hrValue} bpm");
        };

        // Request notifications / enable them
        await hrChar.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);

        Console.WriteLine("Waiting for notifications. Press Enter to exit...");
        Console.ReadLine();

        // Cleanup
        await device.DisconnectAsync();
    }
}*/
/*
class AmazfitBipService
{
    // Replace with the known MAC if possible, or use part of the DeviceId
    static string knownDeviceIdPart = "12:34:56:78:9A:BC".Replace(":", "").ToLower(); // Just the MAC part

    // Heart Rate Service and Measurement UUIDs
    static readonly Guid HeartRateServiceUuid = Guid.Parse("0000180D-0000-1000-8000-00805f9b34fb");
    static readonly Guid HeartRateMeasurementUuid = Guid.Parse("00002A37-0000-1000-8000-00805f9b34fb");

    public static async Task Run()
    {
        Console.WriteLine("Scanning for BLE devices...");

        var devices = await Bluetooth.ScanForDevicesAsync();

        var targetDevice = devices.FirstOrDefault(d =>
            d.Id.ToLower().Contains(knownDeviceIdPart) || // Try to match MAC part in DeviceId
            (d.Name != null && d.Name.ToLower().Contains("bip"))); // Optional: match name

        if (targetDevice == null)
        {
            Console.WriteLine("Device not found.");
            return;
        }

        Console.WriteLine($"Found device: {targetDevice.Name}, Id: {targetDevice.Id}");

        Console.WriteLine("Connecting and discovering services...");

        var services = await targetDevice.GetGattServicesAsync();
        if (services == null || services.Count == 0)
        {
            Console.WriteLine("Failed to discover services. Device may be unreachable or not supporting GATT.");
            return;
        }

        var hrService = services.FirstOrDefault(s => s.Uuid == HeartRateServiceUuid);
        if (hrService == null)
        {
            Console.WriteLine("Heart Rate service not found.");
            return;
        }

        var characteristics = await hrService.GetCharacteristicsAsync();
        var hrCharacteristic = characteristics.FirstOrDefault(c => c.Uuid == HeartRateMeasurementUuid);
        if (hrCharacteristic == null)
        {
            Console.WriteLine("Heart Rate Measurement characteristic not found.");
            return;
        }

        Console.WriteLine("Subscribing to heart rate notifications...");

        await hrCharacteristic.StartNotificationsAsync();

        hrCharacteristic.ValueChanged += (sender, args) =>
        {
            var data = args.Value;
            if (data.Length > 1)
            {
                int flags = data[0];
                int hrValue = (flags & 0x01) == 0 ? data[1] : (data[1] | (data[2] << 8));

                Console.WriteLine($"Heart Rate: {hrValue} bpm");
            }
        };

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();

        await hrCharacteristic.StopNotificationsAsync();
    }
}

class Tester
{
    public static async Task Run()
    {
        try
        {
            Debug.WriteLine("Requesting Bluetooth Device...");
            var device = await Bluetooth.RequestDeviceAsync(new RequestDeviceOptions { AcceptAllDevices = true });
            await PrintDeviceInfo(device);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            Console.WriteLine($"{ex.StackTrace}");
        }

        try
        {
            // List devices
            Console.WriteLine("\nScanning for devices...");
            var devices = await Bluetooth.ScanForDevicesAsync();
            foreach (var device in devices)
            {
                Console.WriteLine($"Found device:");
                await PrintDeviceInfo(device);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            Console.WriteLine($"{ex.StackTrace}");
        }

        try
        {

            // Pick a device
            Console.WriteLine("\nPicking a deivce");
            BluetoothDevicePicker picker = new();
            var deviceInfo = await picker.PickSingleDeviceAsync();
            if (deviceInfo != null)
            {
                Console.WriteLine($"Picked device:");
                Console.WriteLine($"  address = {deviceInfo.DeviceAddress}");
                Console.WriteLine($"  name = {deviceInfo.DeviceName}");
                Console.WriteLine($"  class = {deviceInfo.ClassOfDevice}");
                Console.WriteLine($"  is connected = {deviceInfo.Connected}");
                Console.WriteLine($"  is authenticated = {deviceInfo.Authenticated}");

                Console.WriteLine("\nConnecting....");
                var device = await BluetoothDevice.FromIdAsync(deviceInfo.DeviceAddress.ToString());

                if (device != null)
                {
                    Console.WriteLine("Connected. Pairing....");
                    await device.PairAsync();
                    if (device.IsPaired)
                    {
                        Console.WriteLine("Paired.");
                        Console.WriteLine("Device info:");
                        await PrintDeviceInfo(device);
                    }
                    else
                    {
                        Console.WriteLine("Failed...");
                    }
                }
            }
            else
            {
                Console.WriteLine("No device was selected. Exiting...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}");
            Console.WriteLine($"{ex.StackTrace}");
        }
    }

    private static async Task PrintDeviceInfo(BluetoothDevice device)
    {
        Console.WriteLine($"  id = {device.Id}");
        Console.WriteLine($"  name = {device.Name}");
        Console.WriteLine($"  is paired = {device.IsPaired}");
        Console.WriteLine($"  is connected = {device.Gatt.IsConnected}");
        Console.WriteLine($"  is auto-connect = {device.Gatt.AutoConnect}");
        Console.WriteLine($"  services:");

        var services = await device.Gatt.GetPrimaryServicesAsync();
        foreach (var service in services)
        {
            Console.WriteLine($"    {service.Uuid}");
            Console.WriteLine($"    chars:");

            var chars = await service.GetCharacteristicsAsync();
            foreach (var c in chars)
            {
                Console.WriteLine($"      {c.Uuid} = {c.Value}");
            }

            Console.WriteLine($"    incls:");

            var incl = await service.GetIncludedServicesAsync();
            foreach (var i in incl)
            {
                Console.WriteLine($"      {i.Uuid}, is primary = {i.IsPrimary}");
            }
        }
    }
}*/