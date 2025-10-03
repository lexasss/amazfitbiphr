import asyncio
from bleak import BleakClient

HR_SERVICE_UUID = "0000180d-0000-1000-8000-00805f9b34fb"
HR_MEASUREMENT_CHAR = "00002a37-0000-1000-8000-00805f9b34fb"
DEFAULT_MAC = "da:81:90:cb:f3:22"

async def notification_handler(sender: int, data: bytearray):
    """
    Handler called when new HR data arrives.
    Format defined in BLE spec: first byte is flags, next is HR value (uint8 or uint16)
    """
    # parse flags
    flags = data[0]
    hr_format_16bit = bool(flags & 0x01)
    if hr_format_16bit:
        hr = int.from_bytes(data[1:3], byteorder="little")
    else:
        hr = data[1]
    print(f"Heart rate: {hr} bpm")

async def run(mac_address: str):
    print("Connecting to", mac_address)
    try:
        async with BleakClient(mac_address) as client:
            print("Connected:", client.is_connected)
            # ensure the HR service is there
            svcs = await client.get_services()
            if HR_SERVICE_UUID not in [s.uuid for s in svcs]:
                print("Heart Rate service not found!")
                return
            print("Subscribing to HR notifications...")
            await client.start_notify(HR_MEASUREMENT_CHAR, notification_handler)
            # keep running
            while True:
                await asyncio.sleep(1.0)
    except Exception as e:
        print("Error:", e)

if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        mac = DEFAULT_MAC
        print(f"MAC address: {mac}")
        print("{Run 'python __main__.py <MAC_ADDRESS>' to specify a different address)")
    else:
        mac = sys.argv[1]

    asyncio.run(run(mac))