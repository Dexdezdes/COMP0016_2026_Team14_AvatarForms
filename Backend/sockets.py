import websockets
import asyncio
import json

from formatting import bcolors

connected_clients = set()

async def stream_message(message_type, content):
    if connected_clients:
        message = json.dumps({
            "type": message_type,
            "content": content
        })

        await asyncio.gather(
            *[client.send(message) for client in connected_clients],
            return_exceptions=True
        )

async def websocket_handler(websocket):
    connected_clients.add(websocket)
    print(f"{bcolors.OKGREEN}Browser connected. Total clients: {len(connected_clients)}{bcolors.ENDC}")
    try:
        async for message in websocket:
            print(f"Received from client: {message}")
    except websockets.exceptions.ConnectionClosed:
        pass
    except Exception as e:
        print(f"{bcolors.FAIL}WebSocket error: {e}{bcolors.ENDC}")
    finally:
        connected_clients.discard(websocket)
        print(f"{bcolors.WARNING}Browser disconnected. Total clients: {len(connected_clients)}{bcolors.ENDC}")

async def start_server():
    try:
        server = await websockets.serve(websocket_handler, "0.0.0.0", 8883)
        print(f"{bcolors.OKGREEN}WebSocket server started on ws://localhost:8883{bcolors.ENDC}")
        return server
    except Exception as e:
        print(f"{bcolors.FAIL}Failed to start WebSocket server: {e}{bcolors.ENDC}")
        raise