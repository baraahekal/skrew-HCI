#!/usr/bin/env python3
import asyncio
import socket
from bleak import BleakScanner, BleakClient

async def pair_device():
    print("Scanning for Bluetooth devices...")
    devices = await BleakScanner.discover()
    selected_device = None
    user_id = None

    # Iterate over devices to find one with "baraa" or "xm4" in its name.
    for device in devices:
        name = device.name
        if name is None:
            continue
        lname = name.lower()
        if "xqwd;lmklkdm" in lname:
            selected_device = device
            user_id = "UserA"
            break
        elif "baraa" in lname:
            selected_device = device
            user_id = "UserB"
            break

    if selected_device is None:
        print("No matching device found.")
        return None

    print(f"Attempting to pair with {selected_device.name} [{selected_device.address}]")
    try:
        async with BleakClient(selected_device) as client:
            if client.is_connected:
                print("Paired successfully!")
                print(f"Assigned User ID: {user_id}")
                return user_id
            else:
                print("Failed to pair with device.")
                return None
    except Exception as e:
        print("Exception during pairing:", e)
        return None

def start_server(user_id):
    HOST = ''      # Listen on all interfaces.
    PORT = 65432   # Use a non-privileged port.
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind((HOST, PORT))
        s.listen(1)
        print(f"Server listening on port {PORT}...")
        conn, addr = s.accept()
        with conn:
            print("Connected by", addr)
            # Send the user_id as UTF-8 bytes.
            conn.sendall(user_id.encode('utf-8'))
            print("Sent user ID:", user_id)

async def main():
    user_id = await pair_device()
    if user_id is not None:
        start_server(user_id)
    else:
        print("Pairing failed. Exiting.")

if __name__ == "__main__":
    asyncio.run(main())

