import argparse
import asyncio
import csv
import os
import time
from datetime import datetime

from bleak import BleakClient, BleakScanner

HR_CHAR = "00002a37-0000-1000-8000-00805f9b34fb"


def parse_hr(data: bytearray) -> int:
    flags = data[0]
    return int.from_bytes(data[1:3], "little") if (flags & 1) else data[1]


def ts():
    return datetime.now().astimezone().isoformat(timespec="seconds")


async def find_addr(addr, name, scan_t):
    if addr:
        return addr

    print(f"Scanning for {name}...")
    devs = await BleakScanner.discover(timeout=scan_t)

    for d in devs:
        print(f"Found: {d.name!r} - {d.address}")

        if d.name and name.lower() in d.name.lower():
            return d.address

    return None


def touch_ready(path: str, text: str):
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)

    with open(path, "w", encoding="utf-8") as f:
        f.write(text)


async def writer(q, csv_path):
    first = not os.path.exists(csv_path)

    with open(csv_path, "a", newline="", encoding="utf-8") as f:
        w = csv.writer(f)

        if first:
            w.writerow(["timestamp", "unix_s", "bpm"])
            f.flush()

        while True:
            row = await q.get()

            if row is None:
                break

            w.writerow(row)
            f.flush()


async def main(a):

    q = asyncio.Queue(maxsize=5000)

    if os.path.exists(a.stop_file):
        os.remove(a.stop_file)

    if a.ready_file and os.path.exists(a.ready_file):
        os.remove(a.ready_file)

    writer_task = asyncio.create_task(writer(q, a.out_csv))

    while True:

        if os.path.exists(a.stop_file):
            break

        try:

            addr = await find_addr(
                a.address,
                a.name,
                a.scan_timeout
            )

            if not addr:
                print("HW706 not found. Retrying...")
                await asyncio.sleep(a.retry_s)
                continue

            print(f"Connecting to HW706 at {addr}...")

            async with BleakClient(
                addr,
                timeout=a.connect_timeout
            ) as c:

                print("Connected!")

                def cb(_, data):
                    try:
                        bpm = parse_hr(bytearray(data))

                        print(f"HR: {bpm} bpm")

                        q.put_nowait(
                            (
                                ts(),
                                f"{time.time():.3f}",
                                bpm
                            )
                        )

                    except Exception as e:
                        print("HR callback error:", repr(e))

                print("Subscribing to heart-rate characteristic...")

                try:
                    await c.start_notify(HR_CHAR, cb)

                except Exception as e:
                    print("FAILED TO SUBSCRIBE")
                    print("Reason:", repr(e))
                    await asyncio.sleep(a.retry_s)
                    continue

                print("Subscribed! Logging HR to CSV.")

                if a.ready_file:
                    touch_ready(
                        a.ready_file,
                        f"READY,{ts()}\nADDR,{addr}\n"
                    )

                while (
                    c.is_connected
                    and not os.path.exists(a.stop_file)
                ):
                    await asyncio.sleep(0.25)

                try:
                    await c.stop_notify(HR_CHAR)
                except Exception:
                    pass

        except Exception as e:

            print("CONNECTION ERROR:")
            print(repr(e))

            await asyncio.sleep(a.retry_s)

        await asyncio.sleep(a.retry_s)

    await q.put(None)
    await writer_task


if __name__ == "__main__":

    p = argparse.ArgumentParser()

    p.add_argument("--out-csv", required=True)
    p.add_argument("--stop-file", required=True)

    p.add_argument("--ready-file", default=None)

    p.add_argument(
        "--address",
        default=None
    )

    p.add_argument(
        "--name",
        default="HW706"
    )

    p.add_argument(
        "--scan-timeout",
        type=float,
        default=6
    )

    p.add_argument(
        "--connect-timeout",
        type=float,
        default=20
    )

    p.add_argument(
        "--retry-s",
        type=float,
        default=2
    )

    asyncio.run(main(p.parse_args()))