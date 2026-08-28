import asyncio
from bleak import BleakScanner, BleakClient

HR_MEASUREMENT_UUID = "00002a37-0000-1000-8000-00805f9b34fb"

def parse_hr_measurement(data: bytearray) -> int:
    flags = data[0]
    hr_16bit = flags & 0x01
    return int.from_bytes(data[1:3], "little") if hr_16bit else data[1]

async def main():
    print("Scanning for BLE devices (10s)...")
    devices = await BleakScanner.discover(timeout=10.0)

    candidates = [d for d in devices if d.name and "hw706" in d.name.lower()]
    if not candidates:
        print("No HW706 found. Nearby devices:")
        for d in devices:
            print(f"  name={d.name!r:25} address={d.address}")
        return

    device = candidates[0]
    print(f"Connecting to: name={device.name!r}, address={device.address}")

    async with BleakClient(device.address) as client:
        def on_hr(sender, data):
            bpm = parse_hr_measurement(bytearray(data))
            print(f"HR: {bpm} bpm")

        # Try subscribing directly (most compatible)
        try:
            await client.start_notify(HR_MEASUREMENT_UUID, on_hr)
        except Exception as e:
            print("Failed to subscribe to standard Heart Rate Measurement (0x2A37).")
            print("Reason:", repr(e))
            print("\nNext step: we will enumerate services/characteristics (see Fix 2).")
            return

        print("Subscribed. Press Ctrl+C to stop.")
        while True:
            await asyncio.sleep(1)

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
